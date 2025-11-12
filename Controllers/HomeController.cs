using Microsoft.AspNetCore.Mvc;
using SearchSGTestApp.Models;
using SearchSGTestApp.Services;
using System.Linq;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using RestSharp;

namespace SearchSGTestApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SearchSGAuthService _authService;
        private readonly SearchSGPushService _pushService;
        private readonly AccessKeyStorageService _storageService;
        private readonly SearchSGAdminService _adminService;
        private readonly SearchSGConfiguration _config;

        public HomeController(ILogger<HomeController> logger, SearchSGAuthService authService, SearchSGPushService pushService, AccessKeyStorageService storageService, SearchSGAdminService adminService, SearchSGConfiguration config)
        {
            _logger = logger;
            _authService = authService;
            _pushService = pushService;
            _storageService = storageService;
            _adminService = adminService;
            _config = config;
        }

        public IActionResult Index()
        {
            var model = new TestDocumentViewModel
            {
                DocumentId = GenerateDocumentId(),
                Url = "https://example.com/test-page",
                Title = "Test Document",
                Content = "This is a test document for SearchSG Push API testing.",
                ContentType = "Pages",
                Categories = "Test,Demo",
                Keywords = "test,demo,searchsg",
                CustomFilter1 = "tag1,tag2",
                SearchRankingScore = 100
            };
            return View(model);
        }

        private string GenerateDocumentId()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var random = new Random().Next(1000, 9999).ToString();
            return $"doc-{timestamp}-{random}";
        }

        [HttpPost]
        public async Task<IActionResult> TestAuthentication()
        {
            try
            {
                var token = await _authService.GetAccessTokenAsync();
                return Json(new ApiTestResult
                {
                    Success = true,
                    Message = "Authentication successful! Token obtained.",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication test failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Authentication failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ShowCurrentKey()
        {
            try
            {
                var savedKeys = await _storageService.GetAccessKeysAsync(_config.ApplicationId);
                
                if (savedKeys == null)
                {
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = "No saved Access Keys found. Please create or get keys first.",
                        ErrorDetails = "Access Keys are saved locally after creation/retrieval.",
                        Timestamp = DateTime.Now
                    });
                }

                return Json(new ApiTestResult
                {
                    Success = true,
                    Message = $"Current saved Access Keys for App ID: {_config.ApplicationId}",
                    ErrorDetails = System.Text.Json.JsonSerializer.Serialize(savedKeys, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Show current key failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Show current key failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateAccessKeys()
        {
            try
            {
                var token = await _authService.GetAccessTokenAsync();

                // Create access keys for current application
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"{_config.BaseUrl}/admin/v1/bootstrap/applications/{_config.ApplicationId}/accessKeys");

                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");
                
                // Create request body with required description field
                var requestBody = new
                {
                    description = $"Access key created via test app - {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                };
                var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
                request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Save the created access key to local storage
                    var keyData = System.Text.Json.JsonSerializer.Deserialize<object>(content);
                    await _storageService.SaveAccessKeysAsync(_config.ApplicationId, keyData);
                }

                return Json(new ApiTestResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ?
                        $"Access key created and saved successfully! App ID: {_config.ApplicationId}" :
                        $"Create access key failed: {response.StatusCode} for App ID: {_config.ApplicationId}",
                    ErrorDetails = response.IsSuccessStatusCode ? content : content,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create access keys failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Create access keys failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetAccessKeys()
        {
            try
            {
                var token = await _authService.GetAccessTokenAsync();

                // Get access keys for current application
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{_config.BaseUrl}/admin/v1/bootstrap/applications/{_config.ApplicationId}/accessKeys");

                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                var message = response.IsSuccessStatusCode ?
                    $"⚠️ DANGEROUS: Retrieved fresh Access Keys from API! App ID: {_config.ApplicationId}" :
                    $"Get access keys failed: {response.StatusCode} for App ID: {_config.ApplicationId}";

                // Add helpful message if no access keys found
                if (response.IsSuccessStatusCode && content.Contains("[]"))
                {
                    message += "\n\nNo Access Keys found on server. Use 'Create Key' to create new ones (Limit: 2 per app).";
                }
                else if (response.IsSuccessStatusCode)
                {
                    // Save the retrieved access keys to local storage
                    var keyData = System.Text.Json.JsonSerializer.Deserialize<object>(content);
                    await _storageService.SaveAccessKeysAsync(_config.ApplicationId, keyData);
                    message += "\n\nKeys saved locally for future use.";
                }

                return Json(new ApiTestResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = message,
                    ErrorDetails = response.IsSuccessStatusCode ? content : content,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get access keys failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Get access keys failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> TestApplicationAccess()
        {
            try
            {
                var (response, content) = await ExecuteWithTokenRetryAsync(async (token) =>
                {
                    var client = new HttpClient();
                    var request = new HttpRequestMessage(HttpMethod.Get,
                        $"{_config.BaseUrl}/admin/v1/bootstrap/applications/{_config.ApplicationId}");

                    request.Headers.Add("Authorization", $"Bearer {token}");
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                    var response = await client.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();
                    
                    return (response, content);
                });

                return Json(new ApiTestResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ?
                        $"Application access successful!" +
                        $"\r\nApp ID: {_config.ApplicationId}" :
                        $"Application access failed: {response.StatusCode} for App ID: {_config.ApplicationId}",
                    ErrorDetails = response.IsSuccessStatusCode ? null : content,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Application access test failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Application access test failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }
        [HttpPost]
        public async Task<IActionResult> ListApplications()
        {
            try
            {
                var (response, content) = await ExecuteWithTokenRetryAsync(async (token) =>
                {
                    var client = new HttpClient();
                    var request = new HttpRequestMessage(HttpMethod.Get,
                        $"{_config.BaseUrl}/admin/v1/bootstrap/applications");

                    request.Headers.Add("Authorization", $"Bearer {token}");
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                    var response = await client.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();
                    
                    return (response, content);
                });

                return Json(new ApiTestResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ?
                        "Applications listed successfully!" :
                        $"List applications failed: {response.StatusCode}",
                    ErrorDetails = response.IsSuccessStatusCode ? content : content,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "List applications failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "List applications failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetAppDetails()
        {
            try
            {
                var token = await _authService.GetAccessTokenAsync();

                // Get specific application details
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{_config.BaseUrl}/admin/v1/bootstrap/applications/{_config.ApplicationId}");

                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                return Json(new ApiTestResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ?
                        $"Application details retrieved successfully! App ID: {_config.ApplicationId}" :
                        $"Get application details failed: {response.StatusCode} for App ID: {_config.ApplicationId}",
                    ErrorDetails = response.IsSuccessStatusCode ? content : content,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get application details failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Get application details failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetIndexStatus()
        {
            try
            {
                var token = await _authService.GetAccessTokenAsync();

                // Get index status for current application
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{_config.BaseUrl}/admin/v1/bootstrap/applications/{_config.ApplicationId}/indexStatus");

                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                return Json(new ApiTestResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ?
                        $"Index status retrieved successfully! App ID: {_config.ApplicationId}" :
                        $"Get index status failed: {response.StatusCode} for App ID: {_config.ApplicationId}",
                    ErrorDetails = response.IsSuccessStatusCode ? content : content,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get index status failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Get index status failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> TriggerIndexSync()
        {
            try
            {
                var token = await _authService.GetAccessTokenAsync();

                // Trigger index sync for current application
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"{_config.BaseUrl}/admin/v1/bootstrap/applications/{_config.ApplicationId}/syncIndex");

                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                return Json(new ApiTestResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ?
                        $"Index sync triggered successfully! App ID: {_config.ApplicationId}" :
                        $"Trigger index sync failed: {response.StatusCode} for App ID: {_config.ApplicationId}",
                    ErrorDetails = response.IsSuccessStatusCode ? content : content,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trigger index sync failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Trigger index sync failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }


        [HttpPost]
        public async Task<IActionResult> SearchDocuments([FromBody] SearchQueryRequest searchRequest)
        {
            try
            {
                // Validate request
                if (searchRequest == null)
                {
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = "Invalid search request",
                        ErrorDetails = "Search request cannot be null",
                        Timestamp = DateTime.Now
                    });
                }

                // Prepare query parameters
                // Keep query empty if not provided or if it's the default "*"
                var query = string.IsNullOrWhiteSpace(searchRequest.Query) || searchRequest.Query.Trim() == "*" 
                    ? "" 
                    : searchRequest.Query.Trim();
                // Keep size as provided
                var size = searchRequest.Size;
                var clientId = _config.ApplicationId;

                if (string.IsNullOrEmpty(clientId))
                {
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = "Application ID is not configured",
                        ErrorDetails = "Please configure ApplicationId in appconfigs.json",
                        Timestamp = DateTime.Now
                    });
                }

                // Get Access Keys for OAuth2 authentication
                var savedKeys = await _storageService.GetAccessKeysAsync(clientId);
                if (savedKeys == null)
                {
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = "Access Keys not found",
                        ErrorDetails = "Please create or retrieve Access Keys first before searching. Use 'Create Access Keys' or 'Get Access Keys' functionality.",
                        Timestamp = DateTime.Now
                    });
                }

                var accessKeyData = _storageService.ParseAccessKeyData(savedKeys);
                if (accessKeyData == null || string.IsNullOrEmpty(accessKeyData.AccessKeyId) || string.IsNullOrEmpty(accessKeyData.AccessKeySecret))
                {
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = "Invalid Access Keys",
                        ErrorDetails = "Access Keys are invalid or incomplete. Please create new Access Keys.",
                        Timestamp = DateTime.Now
                    });
                }

                // Step 1: Get OAuth2 access token from Search API using SearchSGAuthService (with caching)
                string searchAccessToken;
                try
                {
                    searchAccessToken = await _authService.GetSearchApiTokenAsync(accessKeyData.AccessKeyId, accessKeyData.AccessKeySecret);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get Search API access token");
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = "Failed to get Search API access token",
                        ErrorDetails = ex.Message,
                        Timestamp = DateTime.Now
                    });
                }

                // Step 2: Use access token to call Search API with GET request
                var scope = string.IsNullOrWhiteSpace(searchRequest.Scope) ? "domain" : searchRequest.Scope.Trim();

                // Use GET request with query params and Bearer token in Authorization header
                // Build URL with specific order: clientId, scope, size, q, from
                var queryParts = new List<string>
                {
                    $"clientId={Uri.EscapeDataString(clientId)}",
                    $"scope={Uri.EscapeDataString(scope)}"
                };
                
                // Add size - if 0 or not provided, use 10000 to get all documents
                if (searchRequest.Size > 0)
                {
                    queryParts.Add($"size={size}");
                }
                else
                {
                    queryParts.Add("size=10000");  // Use large number instead of empty to get all documents
                }
                
                // Add query - can be empty (q=)
                if (string.IsNullOrWhiteSpace(query))
                {
                    queryParts.Add("q=");
                }
                else
                {
                    queryParts.Add($"q={Uri.EscapeDataString(query)}");
                }
                
                if (searchRequest.From > 0)
                {
                    queryParts.Add($"from={searchRequest.From}");
                }
                var fullUrl = $"{_config.BaseUrl}/search/v1/search?{string.Join("&", queryParts)}";
                _logger.LogInformation("Search API GET request: {Url}", fullUrl);

                // Configure HttpClient to automatically decompress gzip/deflate responses
                using var handler = new System.Net.Http.HttpClientHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.All
                };
                using var httpClient = new HttpClient(handler);
                var httpRequest = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                
                // Set Bearer token in Authorization header
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", searchAccessToken);
                httpRequest.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36");
                httpRequest.Headers.Accept.ParseAdd("application/json");
                
                var httpResponse = await httpClient.SendAsync(httpRequest);
                
                var responseContent = httpResponse.Content != null 
                    ? await httpResponse.Content.ReadAsStringAsync() 
                    : string.Empty;

                // _logger.LogInformation("Search API response: Status={Status}, ContentLength={Length}, Content={Content}",
                //     httpResponse.StatusCode, responseContent.Length, responseContent);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Search API failed: {Status}, {Content}. Request URL: {Url}, Token length: {TokenLength}", 
                        httpResponse.StatusCode, responseContent, fullUrl, searchAccessToken?.Length ?? 0);
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = $"Search API failed: {httpResponse.StatusCode}",
                        ErrorDetails = responseContent,
                        Timestamp = DateTime.Now
                    });
                }

                // Parse and return search results
                try
                {
                    // Parse JSON response
                    using var document = System.Text.Json.JsonDocument.Parse(responseContent);
                    var root = document.RootElement;

                    // Format response for display
                    var formattedResponse = System.Text.Json.JsonSerializer.Serialize(root, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    // Extract summary information and build items list
                    var resultCount = 0;
                    var totalResults = 0;
                    var queryDisplay = string.IsNullOrWhiteSpace(query) ? "(empty)" : query;
                    var sizeDisplay = size > 0 ? size.ToString() : "(all)";
                    var message = $"Search completed successfully. Query: '{queryDisplay}', Size: {sizeDisplay}";
                    var itemsListHtml = "";

                    // Get total number of results
                    if (root.TryGetProperty("totalNumberOfResults", out var totalElement))
                    {
                        totalResults = totalElement.GetInt32();
                        message += $", Found: {totalResults} result(s)";
                    }

                    // Extract and format resultItems
                    if (root.TryGetProperty("resultItems", out var resultItemsElement) && resultItemsElement.ValueKind == JsonValueKind.Array)
                    {
                        resultCount = resultItemsElement.GetArrayLength();
                        
                        // Build HTML list of items with improved styling
                        var itemsHtml = new System.Text.StringBuilder();
                        itemsHtml.AppendLine("<div class='search-results-container mt-4'>");
                        itemsHtml.AppendLine($"<div class='d-flex justify-content-between align-items-center mb-3'>");
                        itemsHtml.AppendLine($"<h5 class='mb-0'><i class='fas fa-list me-2 text-primary'></i>Search Results</h5>");
                        itemsHtml.AppendLine($"<span class='badge bg-primary rounded-pill result-count-badge'>{resultCount} item(s)</span>");
                        itemsHtml.AppendLine("</div>");
                        itemsHtml.AppendLine("<div class='row g-3'>");

                        foreach (var item in resultItemsElement.EnumerateArray())
                        {
                            var documentId = item.TryGetProperty("documentId", out var docId) ? docId.GetString() : "N/A";
                            var title = item.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : "No Title";
                            var url = item.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : "#";
                            var description = item.TryGetProperty("description", out var descEl) ? descEl.GetString() : "";
                            var contentType = item.TryGetProperty("contentType", out var typeEl) ? typeEl.GetString() : "";
                            var lastUpdated = "";
                            if (item.TryGetProperty("lastUpdated", out var lastUpdatedEl))
                            {
                                if (DateTime.TryParse(lastUpdatedEl.GetString(), out var date))
                                {
                                    lastUpdated = date.ToString("MMM dd, yyyy");
                                }
                            }
                            var scoreConfidence = "";
                            if (item.TryGetProperty("scoreConfidence", out var scoreEl))
                            {
                                scoreConfidence = scoreEl.GetString();
                            }
                            
                            // Get categories as array for badges
                            var categoriesList = new List<string>();
                            if (item.TryGetProperty("categories", out var catsEl) && catsEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var cat in catsEl.EnumerateArray())
                                {
                                    var catValue = cat.GetString();
                                    if (!string.IsNullOrEmpty(catValue) && catValue.Trim() != "")
                                    {
                                        categoriesList.Add(catValue.Trim());
                                    }
                                }
                            }

                            // Get content type icon and badge class
                            var contentTypeIcon = GetContentTypeIcon(contentType);
                            var contentTypeBadgeClass = GetContentTypeBadgeClass(contentType);

                            // Escape HTML for title and description (but not icon which is already HTML)
                            title = System.Net.WebUtility.HtmlEncode(title ?? "");
                            description = System.Net.WebUtility.HtmlEncode(description ?? "");
                            var shortDesc = !string.IsNullOrEmpty(description) 
                                ? (description.Length > 150 ? description.Substring(0, 150) + "..." : description)
                                : "";
                            documentId = System.Net.WebUtility.HtmlEncode(documentId ?? "");
                            url = System.Net.WebUtility.HtmlEncode(url ?? "#");

                            itemsHtml.AppendLine("<div class=\"col-12\">");
                            itemsHtml.AppendLine("<div class=\"card h-100 shadow-sm border-0 result-item-card\" style=\"transition: transform 0.2s, box-shadow 0.2s;\">");
                            itemsHtml.AppendLine("<div class=\"card-body p-3\">");
                            
                            // Header with title and content type badge
                            itemsHtml.AppendLine("<div class=\"d-flex justify-content-between align-items-start mb-2\">");
                            itemsHtml.AppendLine($"<h6 class=\"card-title mb-0 flex-grow-1\">");
                            itemsHtml.AppendLine($"<a href=\"{url}\" target=\"_blank\" class=\"text-decoration-none text-primary fw-bold\" style=\"font-size: 1.05rem;\">{title}</a>");
                            itemsHtml.AppendLine("</h6>");
                            if (!string.IsNullOrEmpty(contentType))
                            {
                                itemsHtml.AppendLine($"<span class=\"badge {contentTypeBadgeClass} ms-2 content-type-badge\" data-content-type=\"{System.Net.WebUtility.HtmlEncode(contentType)}\">{contentTypeIcon} {System.Net.WebUtility.HtmlEncode(contentType)}</span>");
                            }
                            itemsHtml.AppendLine("</div>");
                            
                            // Description
                            if (!string.IsNullOrEmpty(shortDesc))
                            {
                                itemsHtml.AppendLine($"<p class=\"card-text text-muted mb-2\" style=\"font-size: 0.9rem; line-height: 1.5;\">{shortDesc}</p>");
                            }
                            
                            // Categories badges
                            if (categoriesList.Any())
                            {
                                itemsHtml.AppendLine("<div class=\"mb-2\">");
                                foreach (var category in categoriesList.Take(5)) // Limit to 5 categories
                                {
                                    var encodedCategory = System.Net.WebUtility.HtmlEncode(category);
                                    itemsHtml.AppendLine($"<span class=\"badge bg-secondary me-1 mb-1\" style=\"font-size: 0.75rem;\">{encodedCategory}</span>");
                                }
                                if (categoriesList.Count > 5)
                                {
                                    itemsHtml.AppendLine($"<span class=\"badge bg-light text-dark me-1 mb-1\" style=\"font-size: 0.75rem;\">+{categoriesList.Count - 5} more</span>");
                                }
                                itemsHtml.AppendLine("</div>");
                            }
                            
                            // Footer with metadata
                            itemsHtml.AppendLine("<div class=\"d-flex justify-content-between align-items-center pt-2 border-top\">");
                            itemsHtml.AppendLine("<div class=\"small text-muted\">");
                            itemsHtml.AppendLine($"<i class=\"fas fa-hashtag me-1\"></i><span class=\"font-monospace\" style=\"font-size: 0.8rem;\">{documentId}</span>");
                            itemsHtml.AppendLine("</div>");
                            itemsHtml.AppendLine("<div class=\"small text-muted\">");
                            if (!string.IsNullOrEmpty(lastUpdated))
                            {
                                itemsHtml.AppendLine($"<i class=\"far fa-clock me-1\"></i>{System.Net.WebUtility.HtmlEncode(lastUpdated)}");
                            }
                            if (!string.IsNullOrEmpty(scoreConfidence))
                            {
                                var scoreIcon = scoreConfidence == "HIGH" ? "fa-check-circle text-success" : scoreConfidence == "MEDIUM" ? "fa-exclamation-circle text-warning" : "fa-info-circle text-info";
                                var encodedScore = System.Net.WebUtility.HtmlEncode(scoreConfidence);
                                itemsHtml.AppendLine($" <i class=\"fas {scoreIcon} ms-2\" title=\"Score: {encodedScore}\"></i>");
                            }
                            itemsHtml.AppendLine("</div>");
                            itemsHtml.AppendLine("</div>");
                            
                            itemsHtml.AppendLine("</div>");
                            itemsHtml.AppendLine("</div>");
                            itemsHtml.AppendLine("</div>");
                        }

                        itemsHtml.AppendLine("</div>");
                        itemsHtml.AppendLine("</div>");
                        itemsListHtml = itemsHtml.ToString();
                    }
                    else if (root.TryGetProperty("results", out var resultsElement) && resultsElement.ValueKind == JsonValueKind.Array)
                    {
                        resultCount = resultsElement.GetArrayLength();
                        message += $", Found: {resultCount} result(s)";
                    }
                    else if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                    {
                        resultCount = dataElement.GetArrayLength();
                        message += $", Found: {resultCount} result(s)";
                    }

                    return Json(new ApiTestResult
                    {
                        Success = true,
                        Message = message,
                        ItemsList = itemsListHtml,
                        ErrorDetails = formattedResponse,
                        Timestamp = DateTime.Now
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse search response");
                    // Return raw response if parsing fails
                    var queryDisplay = string.IsNullOrWhiteSpace(query) ? "(empty)" : query;
                    var sizeDisplay = size > 0 ? size.ToString() : "(all)";
                    return Json(new ApiTestResult
                    {
                        Success = true,
                        Message = $"Search completed. Query: '{queryDisplay}', Size: {sizeDisplay}",
                        ErrorDetails = responseContent,
                        Timestamp = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search documents failed: {Error}", ex.Message);
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Search documents failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        private string GetContentTypeIcon(string contentType)
        {
            return contentType?.ToUpper() switch
            {
                "PAGES" => "<i class='fas fa-file-alt'></i>",
                "NEWS" => "<i class='fas fa-newspaper'></i>",
                "EVENTS" => "<i class='fas fa-calendar-alt'></i>",
                "COURSES" => "<i class='fas fa-book'></i>",
                "FAQ" => "<i class='fas fa-question-circle'></i>",
                "SCHOOLS" => "<i class='fas fa-school'></i>",
                _ => "<i class='fas fa-file'></i>"
            };
        }

        private string GetContentTypeBadgeClass(string contentType)
        {
            return contentType?.ToUpper() switch
            {
                "PAGES" => "bg-info",
                "NEWS" => "bg-danger",
                "EVENTS" => "bg-warning text-dark",
                "COURSES" => "bg-success",
                "FAQ" => "bg-primary",
                "SCHOOLS" => "bg-secondary",
                _ => "bg-secondary"
            };
        }

        [HttpPost]
        public async Task<IActionResult> PushDocument([FromBody] TestDocumentViewModel model)
        {
            try
            {
                var document = new DocumentToAdd
                {
                    DocumentId = model.DocumentId,
                    Url = model.Url,
                    Title = model.Title,
                    Content = model.Content,
                    LastUpdated = DateTime.UtcNow,
                    ContentType = model.ContentType,
                    Categories = !string.IsNullOrEmpty(model.Categories) ?
                    model.Categories.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToArray() :
                    new string[0],
                    Keywords = !string.IsNullOrEmpty(model.Keywords) ?
                    model.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToArray() :
                    new string[0],
                    CustomFilter1 = !string.IsNullOrEmpty(model.CustomFilter1) ?
                    model.CustomFilter1.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToArray() :
                    new string[0],
                    SearchRankingScore = model.SearchRankingScore
                };

                var request = new PushDocumentsRequest
                {
                    DocumentsToAdd = new List<DocumentToAdd> { document }
                };

                var jobResponse = await _pushService.PushDocumentsAsync(request);

                var result = new ApiTestResult
                {
                    Success = true,
                    Message = $"Document pushed successfully! {jobResponse.Message}",
                    JobId = jobResponse.JobId,
                    Timestamp = DateTime.UtcNow
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Push document test failed");
                var result = new ApiTestResult
                {
                    Success = false,
                    Message = "Push document failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
                return Json(result);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CheckJobStatus([FromBody] string jobId)
        {
            try
            {
                var jobStatus = await _pushService.GetJobStatusAsync(jobId);

                var result = new ApiTestResult
                {
                    Success = true,
                    Message = $"Job Status: {jobStatus.Status}. Created: {jobStatus.CreatedAt:yyyy-MM-dd HH:mm:ss}, Updated: {jobStatus.UpdatedAt:yyyy-MM-dd HH:mm:ss}",
                    JobId = jobStatus.JobId,
                    Timestamp = DateTime.UtcNow
                };

                // Update message to show job status more clearly
                if (jobStatus.Status == "COMPLETED_WITH_ERROR")
                {
                    result.Message = $"Job Status: {jobStatus.Status} (Completed with some failures). Created: {jobStatus.CreatedAt:yyyy-MM-dd HH:mm:ss}, Updated: {jobStatus.UpdatedAt:yyyy-MM-dd HH:mm:ss}";
                }

                if (jobStatus.FailedDocumentsToAdd.Any() || jobStatus.FailedDocumentsToDelete.Any())
                {
                    var failedAddCount = jobStatus.FailedDocumentsToAdd.Count;
                    var failedDeleteCount = jobStatus.FailedDocumentsToDelete.Count;
                    var errorDetails = new System.Text.StringBuilder();
                    errorDetails.AppendLine($"Summary: {failedAddCount} document(s) failed to add, {failedDeleteCount} document(s) failed to delete.");
                    
                    if (failedDeleteCount > 0)
                    {
                        errorDetails.AppendLine("\nFailed documents to delete:");
                        foreach (var failedDoc in jobStatus.FailedDocumentsToDelete)
                        {
                            var docId = failedDoc.DocumentId ?? "Unknown";
                            var errorMsg = failedDoc.Error ?? failedDoc.Message;
                            if (string.IsNullOrEmpty(errorMsg))
                            {
                                errorDetails.AppendLine($"  - {docId} (Document may not exist or was already deleted)");
                            }
                            else
                            {
                                errorDetails.AppendLine($"  - {docId}");
                                errorDetails.AppendLine($"    Error: {errorMsg}");
                            }
                        }
                    }
                    
                    if (failedAddCount > 0)
                    {
                        errorDetails.AppendLine("\nFailed documents to add:");
                        foreach (var failedDoc in jobStatus.FailedDocumentsToAdd)
                        {
                            errorDetails.AppendLine($"  - {failedDoc.DocumentId}");
                        }
                    }
                    
                    result.ErrorDetails = errorDetails.ToString();
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Check job status failed for JobId: {JobId}", jobId);
                var result = new ApiTestResult
                {
                    Success = false,
                    Message = "Check job status failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
                return Json(result);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDocument([FromBody] string documentId)
        {
            try
            {
                var request = new PushDocumentsRequest
                {
                    DocumentsToDelete = new List<string> { documentId }
                };

                var jobResponse = await _pushService.PushDocumentsAsync(request);

                var result = new ApiTestResult
                {
                    Success = true,
                    Message = $"Document deletion queued! {jobResponse.Message}",
                    JobId = jobResponse.JobId,
                    Timestamp = DateTime.UtcNow
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete document test failed for DocumentId: {DocumentId}", documentId);
                var result = new ApiTestResult
                {
                    Success = false,
                    Message = "Delete document failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
                return Json(result);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMultipleDocuments([FromBody] List<string> documentIds)
        {
            try
            {
                if (documentIds == null || documentIds.Count == 0)
                {
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = "No document IDs provided",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Remove any empty or whitespace entries
                var cleanedIds = documentIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                    .Distinct()
                    .ToList();

                if (cleanedIds.Count == 0)
                {
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = "No valid document IDs found",
                        Timestamp = DateTime.UtcNow
                    });
                }

                var request = new PushDocumentsRequest
                {
                    DocumentsToDelete = cleanedIds
                };

                var jobResponse = await _pushService.PushDocumentsAsync(request);

                var result = new ApiTestResult
                {
                    Success = true,
                    Message = $"Bulk deletion queued for {cleanedIds.Count} document(s)! {jobResponse.Message}",
                    JobId = jobResponse.JobId,
                    Timestamp = DateTime.UtcNow,
                    ErrorDetails = $"Document IDs to delete:\n{string.Join("\n", cleanedIds)}"
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk delete documents test failed. Count: {Count}", documentIds?.Count ?? 0);
                var result = new ApiTestResult
                {
                    Success = false,
                    Message = "Bulk delete documents failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
                return Json(result);
            }
        }

        [HttpPost]
        public async Task<IActionResult> PushAndWait([FromBody] TestDocumentViewModel model)
        {
            try
            {
                var document = new DocumentToAdd
                {
                    DocumentId = model.DocumentId,
                    Url = model.Url,
                    Title = model.Title,
                    Content = model.Content,
                    LastUpdated = DateTime.UtcNow,
                    ContentType = model.ContentType,
                    Categories = model.Categories.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                              .Select(c => c.Trim()).ToArray(),
                    Keywords = !string.IsNullOrEmpty(model.Keywords) ?
                    model.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToArray() :
                    new string[0],
                    CustomFilter1 = !string.IsNullOrEmpty(model.CustomFilter1) ?
                    model.CustomFilter1.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).ToArray() :
                    new string[0],
                    SearchRankingScore = model.SearchRankingScore
                };

                var request = new PushDocumentsRequest
                {
                    DocumentsToAdd = new List<DocumentToAdd> { document }
                };

                var jobResponse = await _pushService.PushDocumentsAsync(request);
                var completed = await _pushService.WaitForJobCompletionAsync(jobResponse.JobId, 2);

                var result = new ApiTestResult
                {
                    Success = completed,
                    Message = completed ?
                        $"Document pushed and indexed successfully! JobId: {jobResponse.JobId}" :
                        $"Document pushed but indexing did not complete within timeout. JobId: {jobResponse.JobId}",
                    JobId = jobResponse.JobId,
                    Timestamp = DateTime.UtcNow
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Push and wait test failed");
                var result = new ApiTestResult
                {
                    Success = false,
                    Message = "Push and wait failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
                return Json(result);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CheckAdminPermissions()
        {
            try
            {
                var token = await _authService.GetAccessTokenAsync();

                // Try to access the application endpoint first
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{_config.BaseUrl}/admin/v1/bootstrap/applications/{_config.ApplicationId}");

                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return Json(new ApiTestResult
                    {
                        Success = true,
                        Message = $"✅ You have access to application {_config.ApplicationId}",
                        ErrorDetails = "You can proceed to manage admins for this application.",
                        Timestamp = DateTime.Now
                    });
                }
                else
                {
                    var errorMsg = $"❌ Access denied to application {_config.ApplicationId}";
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        errorMsg += "\n\nPossible solutions:\n" +
                                   "1. Make sure you are an admin of this application\n" +
                                   "2. Check if the Application ID is correct\n" +
                                   "3. Verify your authentication credentials\n" +
                                   "4. Contact the application owner to add you as admin";
                    }

                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = errorMsg,
                        ErrorDetails = $"HTTP {response.StatusCode}: {content}",
                        Timestamp = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Check admin permissions failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Check admin permissions failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetCurrentAdmins()
        {
            try
            {
                var appDetails = await _adminService.GetApplicationDetailsAsync(_config.ApplicationId);

                // Extract adminList from dynamic object
                var adminList = new List<string>();
                try
                {
                    var jsonElement = (JsonElement)appDetails;
                    if (jsonElement.TryGetProperty("data", out var dataElement) &&
                        dataElement.TryGetProperty("tenant", out var tenantElement) &&
                        tenantElement.TryGetProperty("adminList", out var adminListElement))
                    {
                        foreach (var item in adminListElement.EnumerateArray())
                        {
                            adminList.Add(item.GetString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing adminList");
                }
                var adminCount = adminList.Count;
                
                var message = $"Found {adminCount} administrator(s) for App ID: {_config.ApplicationId}";
                
                // Format admin list for display
                string adminListDisplay;
                if (adminCount == 0)
                {
                    adminListDisplay = "No administrators found.";
                }
                else
                {
                    adminListDisplay = "Administrators:\n" + string.Join("\n", adminList.Select((email, index) => $"{index + 1}. {email}"));
                }

                return Json(new ApiTestResult
                {
                    Success = true,
                    Message = message,
                    ErrorDetails = adminListDisplay,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get current admins failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Get current admins failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddAdmin([FromBody] AdminManagementRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email))
                {
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = "Email is required",
                        ErrorDetails = "Please provide a valid email address",
                        Timestamp = DateTime.Now
                    });
                }

                var success = await _adminService.AddAdminToApplicationAsync(_config.ApplicationId, request.Email);

                return Json(new ApiTestResult
                {
                    Success = success,
                    Message = success ? 
                        $"Admin {request.Email} added successfully to App ID: {_config.ApplicationId}" :
                        $"Failed to add admin {request.Email}",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Add admin failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Add admin failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveAdmin([FromBody] AdminManagementRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email))
                {
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = "Email is required",
                        ErrorDetails = "Please provide a valid email address",
                        Timestamp = DateTime.Now
                    });
                }

                var success = await _adminService.RemoveAdminFromApplicationAsync(_config.ApplicationId, request.Email);

                return Json(new ApiTestResult
                {
                    Success = success,
                    Message = success ? 
                        $"Admin {request.Email} removed successfully from App ID: {_config.ApplicationId}" :
                        $"Failed to remove admin {request.Email}",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Remove admin failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Remove admin failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// Helper method to handle INACTIVE_SESSION errors and auto-retry with fresh token
        /// </summary>
        private async Task<(HttpResponseMessage response, string content)> ExecuteWithTokenRetryAsync(
            Func<string, Task<(HttpResponseMessage, string)>> apiCall,
            bool useAdminToken = true)
        {
            // Get initial token
            var token = useAdminToken 
                ? await _authService.GetAccessTokenAsync() 
                : throw new NotImplementedException("Search API token retry not implemented yet");

            // Execute API call
            var (response, content) = await apiCall(token);

            // Check for INACTIVE_SESSION error and retry once with fresh token
            if (!response.IsSuccessStatusCode && content.Contains("INACTIVE_SESSION"))
            {
                _logger.LogWarning("Received INACTIVE_SESSION error, clearing token cache and retrying...");
                
                if (useAdminToken)
                {
                    _authService.ClearToken();
                    token = await _authService.GetAccessTokenAsync();
                }

                // Retry the API call with fresh token
                (response, content) = await apiCall(token);
            }

            return (response, content);
        }
    }

    // Request model for admin management
    public class AdminManagementRequest
    {
        public string Email { get; set; }
    }
}
