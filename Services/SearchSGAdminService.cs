using System.Text;
using System.Text.Json;

namespace SearchSGTestApp.Services
{
    public class SearchSGAdminService
    {
        private readonly HttpClient _httpClient;
        private readonly SearchSGAuthService _authService;
        private readonly string _baseUrl = "https://api.services.search.gov.sg";

        public SearchSGAdminService(HttpClient httpClient, SearchSGAuthService authService)
        {
            _httpClient = httpClient;
            _authService = authService;
        }

        public async Task<dynamic> GetApplicationDetailsAsync(string applicationId)
        {
            try
            {
                var token = await _authService.GetAccessTokenAsync();
                
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"{_baseUrl}/admin/v1/bootstrap/applications/{applicationId}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Debug: Log the raw JSON response to understand the actual structure
                    Console.WriteLine($"🔍 GET Response JSON: {content}");
                    
                    // Use dynamic to avoid deserialization issues
                    return JsonSerializer.Deserialize<dynamic>(content);
                }
                else
                {
                    // Log more details for debugging
                    var errorMessage = $"Failed to get application details: {response.StatusCode} - {content}";
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        errorMessage += "\n\nPossible causes:\n" +
                                      "1. Current user is not an admin of this application\n" +
                                      "2. Token doesn't have sufficient permissions\n" +
                                      "3. Application ID is incorrect\n" +
                                      "4. Token has expired";
                    }
                    throw new Exception(errorMessage);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting application details: {ex.Message}", ex);
            }
        }

        public async Task<bool> AddAdminToApplicationAsync(string applicationId, string newAdminEmail)
        {
            try
            {
                // SAFETY: Get current application details and backup adminList
                var currentApp = await GetApplicationDetailsAsync(applicationId);
                
                // Step 2: Extract and modify adminList from dynamic object
                var currentAdminList = new List<string>();
                try
                {
                    var jsonElement = (JsonElement)currentApp;
                    if (jsonElement.TryGetProperty("data", out var dataElement) &&
                        dataElement.TryGetProperty("tenant", out var tenantElement) &&
                        tenantElement.TryGetProperty("adminList", out var adminListElement))
                    {
                        foreach (var item in adminListElement.EnumerateArray())
                        {
                            currentAdminList.Add(item.GetString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing adminList in AddAdmin: {ex.Message}");
                }
                
                // SAFETY: Backup original admin list
                var originalAdminList = new List<string>(currentAdminList);
                Console.WriteLine($"SAFETY BACKUP - Original AdminList: [{string.Join(", ", originalAdminList)}]");
                
                var updatedAdminList = new List<string>(currentAdminList);
                
                // SAFETY: Validate we have existing admins
                if (currentAdminList.Count == 0)
                {
                    throw new Exception("SAFETY ERROR: No existing admins found! Cannot proceed to avoid lockout.");
                }
                
                if (!updatedAdminList.Contains(newAdminEmail))
                {
                    updatedAdminList.Add(newAdminEmail);
                    Console.WriteLine($"SAFETY CHECK - Adding '{newAdminEmail}' to list of {originalAdminList.Count} existing admins");
                }
                else
                {
                    throw new Exception($"Admin {newAdminEmail} already exists in the admin list");
                }
                
                // SAFETY: Final validation
                if (updatedAdminList.Count != originalAdminList.Count + 1)
                {
                    throw new Exception($"SAFETY ERROR: Admin count mismatch! Expected {originalAdminList.Count + 1}, got {updatedAdminList.Count}");
                }

                // Step 3: Update application with new admin list
                return await UpdateApplicationAsync(applicationId, currentApp, updatedAdminList);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error adding admin to application: {ex.Message}", ex);
            }
        }

        public async Task<bool> RemoveAdminFromApplicationAsync(string applicationId, string adminEmail)
        {
            try
            {
                // Step 1: Get current application details
                var currentApp = await GetApplicationDetailsAsync(applicationId);
                
                // Step 2: Extract and modify adminList from dynamic object
                var currentAdminList = new List<string>();
                try
                {
                    var jsonElement = (JsonElement)currentApp;
                    if (jsonElement.TryGetProperty("data", out var dataElement) &&
                        dataElement.TryGetProperty("tenant", out var tenantElement) &&
                        tenantElement.TryGetProperty("adminList", out var adminListElement))
                    {
                        foreach (var item in adminListElement.EnumerateArray())
                        {
                            currentAdminList.Add(item.GetString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing adminList in RemoveAdmin: {ex.Message}");
                }
                
                // SAFETY: Backup and validate
                var originalAdminList = new List<string>(currentAdminList);
                Console.WriteLine($"SAFETY BACKUP - Original AdminList: [{string.Join(", ", originalAdminList)}]");
                
                // SAFETY: Prevent removing last admin (lockout protection)
                if (currentAdminList.Count <= 1)
                {
                    throw new Exception("SAFETY ERROR: Cannot remove the last admin! This would lock everyone out of the application.");
                }
                
                var updatedAdminList = new List<string>(currentAdminList);
                
                if (updatedAdminList.Contains(adminEmail))
                {
                    updatedAdminList.Remove(adminEmail);
                    Console.WriteLine($"SAFETY CHECK - Removing '{adminEmail}' from list of {originalAdminList.Count} admins. {updatedAdminList.Count} will remain.");
                }
                else
                {
                    throw new Exception($"Admin {adminEmail} not found in the admin list");
                }
                
                // SAFETY: Final validation
                if (updatedAdminList.Count != originalAdminList.Count - 1)
                {
                    throw new Exception($"SAFETY ERROR: Admin count mismatch! Expected {originalAdminList.Count - 1}, got {updatedAdminList.Count}");
                }
                
                // SAFETY: Ensure we still have admins
                if (updatedAdminList.Count == 0)
                {
                    throw new Exception("SAFETY ERROR: Removal would result in zero admins! Operation blocked.");
                }

                // Step 3: Update application with new admin list
                return await UpdateApplicationAsync(applicationId, currentApp, updatedAdminList);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error removing admin from application: {ex.Message}", ex);
            }
        }

        private async Task<bool> UpdateApplicationAsync(string applicationId, dynamic currentApp, List<string> newAdminList)
        {
            try
            {
                var token = await _authService.GetAccessTokenAsync();

                // Use PUT with complete payload (as per SearchSG documentation)
                return await UpdateApplicationWithPutAsync(applicationId, currentApp, newAdminList, token);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating application: {ex.Message}", ex);
            }
        }

        private async Task<bool> UpdateApplicationWithPutAsync(string applicationId, dynamic currentApp, List<string> newAdminList, string token)
        {
            // STRATEGY 1: Try minimal payload first (only adminList)
            Console.WriteLine("🧪 EXPERIMENT - Trying minimal payload approach...");
            
            var minimalPayload = new
            {
                tenant = new
                {
                    adminList = newAdminList
                }
            };

            var success = await TryPutRequest(applicationId, minimalPayload, token, "MINIMAL");
            if (success) return true;

            // STRATEGY 2: Try with name + tenant only
            Console.WriteLine("🧪 EXPERIMENT - Trying name + tenant approach...");
            
            var nameAndTenantPayload = new
            {
                name = "MOE Corp - Push API App - (Optical Dev)",
                tenant = new
                {
                    adminList = newAdminList
                }
            };

            success = await TryPutRequest(applicationId, nameAndTenantPayload, token, "NAME_TENANT");
            if (success) return true;

            // STRATEGY 3: Skip index completely, only application
            Console.WriteLine("🧪 EXPERIMENT - Trying without index...");
            
            var noIndexPayload = new
            {
                name = "MOE Corp - Push API App - (Optical Dev)",
                tenant = new
                {
                    adminList = newAdminList
                },
                application = new
                {
                    siteDomain = "govtech-moe-poc.app.tc1.airbase.sg",
                    environment = "non-production",
                    config = new
                    {
                        search = new
                        {
                            theme = new
                            {
                                primary = "#154999",
                                fontFamily = "Open Sans"
                            }
                        }
                    }
                }
            };

            success = await TryPutRequest(applicationId, noIndexPayload, token, "NO_INDEX");
            if (success) return true;

            // STRATEGY 4: Last resort - contact SearchSG support
            throw new Exception("❌ ALL STRATEGIES FAILED - This appears to be a server-side issue. Please contact SearchSG support to check the application's index configuration.");
        }

        private async Task<bool> TryPutRequest(string applicationId, object payload, string token, string strategy)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });

                Console.WriteLine($"🧪 {strategy} - Payload: {json}");

                var request = new HttpRequestMessage(HttpMethod.Put, 
                    $"{_baseUrl}/admin/v1/bootstrap/applications/{applicationId}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"🧪 {strategy} - Response: {response.StatusCode} - {content}");

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ {strategy} strategy SUCCESS!");
                    return true;
                }
                else
                {
                    Console.WriteLine($"❌ {strategy} strategy failed: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {strategy} strategy exception: {ex.Message}");
                return false;
            }
        }

        private object ExtractCleanConfig(JsonElement configElement)
        {
            try
            {
                // Only extract allowed config fields (no chatbot, etc.)
                var cleanConfig = new
                {
                    search = configElement.TryGetProperty("search", out var searchProp) ? 
                        ExtractCleanSearchConfig(searchProp) :
                        new { theme = new { primary = "#154999", fontFamily = "Open Sans" } }
                };
                return cleanConfig;
            }
            catch
            {
                // Fallback to default
                return new { search = new { theme = new { primary = "#154999", fontFamily = "Open Sans" } } };
            }
        }

        private object ExtractCleanSearchConfig(JsonElement searchElement)
        {
            try
            {
                // Only extract theme - no other properties
                var cleanSearch = new
                {
                    theme = searchElement.TryGetProperty("theme", out var themeProp) ?
                        ExtractCleanTheme(themeProp) :
                        new { primary = "#154999", fontFamily = "Open Sans" }
                };
                return cleanSearch;
            }
            catch
            {
                return new { theme = new { primary = "#154999", fontFamily = "Open Sans" } };
            }
        }

        private object ExtractCleanTheme(JsonElement themeElement)
        {
            try
            {
                var cleanTheme = new
                {
                    primary = themeElement.TryGetProperty("primary", out var primaryProp) ? primaryProp.GetString() : "#154999",
                    fontFamily = themeElement.TryGetProperty("fontFamily", out var fontProp) ? fontProp.GetString() : "Open Sans"
                };
                return cleanTheme;
            }
            catch
            {
                return new { primary = "#154999", fontFamily = "Open Sans" };
            }
        }
    }

    // Response models
    public class ApplicationDetailsResponse
    {
        public ApplicationData Data { get; set; }
    }

    public class ApplicationData
    {
        public int AgencyId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ApplicationTenant Tenant { get; set; }
        public ApplicationIndex Index { get; set; }
        public ApplicationConfig Application { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ApplicationTenant
    {
        public List<string> AdminList { get; set; }
    }

    public class ApplicationIndex
    {
        public string IndexType { get; set; }
        public object DataSource { get; set; }
    }

    public class ApplicationConfig
    {
        public string SiteDomain { get; set; }
        public string Environment { get; set; }
        public object Config { get; set; }
    }
}
