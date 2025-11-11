using Microsoft.AspNetCore.Mvc;
using SearchSGTestApp.Models;
using SearchSGTestApp.Services;
using System.Linq;
using System.Text.Json;

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
                var token = await _authService.GetAccessTokenAsync();

                // Test if we can access the application endpoint
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
                var token = await _authService.GetAccessTokenAsync();

                // List all accessible applications
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{_config.BaseUrl}/admin/v1/bootstrap/applications");

                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

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
                // Search Query API requires Access Keys, not Bearer token
                // First try to get Access Keys, then use them for search
                var token = await _authService.GetAccessTokenAsync();

                // Get Access Keys first
                var client = new HttpClient();
                var accessKeysRequest = new HttpRequestMessage(HttpMethod.Get,
                    $"{_config.BaseUrl}/admin/v1/bootstrap/applications/{_config.ApplicationId}/accessKeys");

                accessKeysRequest.Headers.Add("Authorization", $"Bearer {token}");
                accessKeysRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                var accessKeysResponse = await client.SendAsync(accessKeysRequest);
                var accessKeysContent = await accessKeysResponse.Content.ReadAsStringAsync();

                if (!accessKeysResponse.IsSuccessStatusCode)
                {
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = $"Failed to get Access Keys: {accessKeysResponse.StatusCode}",
                        ErrorDetails = accessKeysContent,
                        Timestamp = DateTime.Now
                    });
                }

                // Try to get saved Access Keys from local storage first (preferred)
                var savedKeys = await _storageService.GetAccessKeysAsync(_config.ApplicationId);
                AccessKeyData? keyData = null;

                if (savedKeys != null)
                {
                    // Use helper method to parse Access Keys from any format
                    keyData = _storageService.ParseAccessKeyData(savedKeys);
                }

                // If no saved keys or parsing failed, try to get from API response
                if (keyData == null)
                {
                    try
                    {
                        // Parse Access Keys response - handle both object and array formats
                        using var document = System.Text.Json.JsonDocument.Parse(accessKeysContent);
                        var root = document.RootElement;
                        
                        // Parse from API response using the helper method (it handles all formats)
                        keyData = _storageService.ParseAccessKeyData(root);
                        
                        // Save for next time if successfully parsed
                        if (keyData != null)
                        {
                            await _storageService.SaveAccessKeysAsync(_config.ApplicationId, accessKeysContent);
                            _logger.LogInformation("Access Keys parsed from API response and saved to local storage");
                        }
                        else
                        {
                            // Check if we have access keys in the response but couldn't parse them
                            bool hasKeys = false;
                            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                            {
                                hasKeys = true;
                            }
                            else if (root.ValueKind == JsonValueKind.Object && 
                                     root.TryGetProperty("data", out var dataProp) && 
                                     dataProp.ValueKind == JsonValueKind.Array && 
                                     dataProp.GetArrayLength() > 0)
                            {
                                hasKeys = true;
                            }
                            
                            if (hasKeys)
                            {
                                return Json(new ApiTestResult
                                {
                                    Success = false,
                                    Message = "Access Keys found but failed to parse. Please check the data format.",
                                    ErrorDetails = "Access Keys exist in API response but could not be parsed. Please use 'Show Current Key' to verify format.",
                                    Timestamp = DateTime.Now
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse Access Keys from API response: {Error}", ex.Message);
                        return Json(new ApiTestResult
                        {
                            Success = false,
                            Message = "Failed to parse Access Keys response from API",
                            ErrorDetails = $"Parse error: {ex.Message}",
                            Timestamp = DateTime.Now
                        });
                    }
                }

                // Final check - if still no keys, return error
                if (keyData == null)
                {
                    return Json(new ApiTestResult
                    {
                        Success = false,
                        Message = "No Access Keys found. Please create Access Keys first to use Search API.",
                        ErrorDetails = "Search API requires Access Keys for authentication. Use the 'Create Access Keys' feature first.",
                        Timestamp = DateTime.Now
                    });
                }

                if (keyData != null && !string.IsNullOrEmpty(keyData.AccessKeyId))
                {
                    // TODO: Implement proper Search API with Access Keys
                    // For now, show that we have the keys and can proceed
                    return Json(new ApiTestResult
                    {
                        Success = true,
                        Message = $"Ready to search with Access Key: {keyData.AccessKeyId}",
                        ErrorDetails = $"Search implementation with Access Keys is ready. AccessKeyId: {keyData.AccessKeyId}",
                        Timestamp = DateTime.Now
                    });
                }

                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "No saved Access Keys found. Please use 'Show Current Key' to verify or 'Create Key' to create new ones.",
                    ErrorDetails = "Search API requires Access Keys for authentication. Use local storage keys to avoid hitting API limits.",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search documents failed");
                return Json(new ApiTestResult
                {
                    Success = false,
                    Message = "Search documents failed",
                    ErrorDetails = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
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
                    Console.WriteLine($"Error parsing adminList: {ex.Message}");
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
    }

    // Request model for admin management
    public class AdminManagementRequest
    {
        public string Email { get; set; }
    }
}
