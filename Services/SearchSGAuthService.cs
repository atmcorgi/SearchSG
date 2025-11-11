using SearchSGTestApp.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SearchSGTestApp.Services
{
    public class SearchSGAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SearchSGConfiguration _config;
        private readonly ILogger<SearchSGAuthService> _logger;
        private string? _cachedToken;
        private DateTime _tokenExpiry;
        
        // Separate cache for Search API token (OAuth2 with Access Keys)
        // Cache is keyed by AccessKeyId to support multiple access keys
        private readonly Dictionary<string, (string token, DateTime expiry)> _cachedSearchApiTokens = new();
        private readonly object _cacheLock = new object();

        public SearchSGAuthService(IHttpClientFactory httpClientFactory, SearchSGConfiguration config, ILogger<SearchSGAuthService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            // Check if cached token is still valid
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
            {
                _logger.LogInformation("Using cached access token");
                return _cachedToken;
            }

            _logger.LogInformation("Requesting new access token from SearchSG Admin API");

            // Validate configuration
            if (string.IsNullOrWhiteSpace(_config.ClientId) || string.IsNullOrWhiteSpace(_config.ClientSecret))
            {
                throw new InvalidOperationException("ClientId or ClientSecret is not configured. Please check appconfigs.json");
            }

            try
            {
                // Create Basic Authentication credentials
                var credentialsString = $"{_config.ClientId}:{_config.ClientSecret}";
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentialsString));

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/admin/v1/auth/token");
                
                // Set Basic Auth header using AuthenticationHeaderValue (standard way)
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36");

                // Admin API token endpoint requires empty body (as per documentation)
                request.Content = new StringContent("", Encoding.UTF8, "application/json");

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get Admin API access token. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, responseContent);
                    throw new HttpRequestException($"Authentication failed: {response.StatusCode}. Response: {responseContent}");
                }

                // Parse token response
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tokenResponse?.AccessToken == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
                {
                    _logger.LogError("No access token received from Admin API. Response: {Response}", responseContent);
                    throw new InvalidOperationException("No access token received from authentication endpoint");
                }

                // Cache token with expiry (Admin API tokens typically expire in 20-30 minutes, 
                // we set 18 minutes to refresh before expiry)
                _cachedToken = tokenResponse.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddMinutes(18);

                _logger.LogInformation("Successfully obtained Admin API access token, cached until {Expiry}", _tokenExpiry);
                return _cachedToken;
            }
            catch (HttpRequestException)
            {
                // Re-throw HttpRequestException as-is (already logged)
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obtaining Admin API access token");
                throw;
            }
        }

        public void ClearToken()
        {
            _cachedToken = null;
            _tokenExpiry = DateTime.MinValue;
            _logger.LogInformation("Access token cache cleared");
        }

        /// <summary>
        /// Get OAuth2 access token for Search API using Access Keys.
        /// This method handles token caching to avoid unnecessary API calls.
        /// Cache is keyed by AccessKeyId to support multiple access keys.
        /// </summary>
        /// <param name="accessKeyId">Access Key ID for authentication</param>
        /// <param name="accessKeySecret">Access Key Secret for authentication</param>
        /// <returns>Access token for Search API</returns>
        public async Task<string> GetSearchApiTokenAsync(string accessKeyId, string accessKeySecret)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(accessKeyId) || string.IsNullOrWhiteSpace(accessKeySecret))
            {
                throw new InvalidOperationException("AccessKeyId or AccessKeySecret is not provided");
            }

            // Check if cached token is still valid (thread-safe)
            lock (_cacheLock)
            {
                if (_cachedSearchApiTokens.TryGetValue(accessKeyId, out var cached) && DateTime.UtcNow < cached.expiry)
                {
                    _logger.LogInformation("Using cached Search API access token for AccessKeyId: {AccessKeyId}", accessKeyId);
                    return cached.token;
                }
            }

            _logger.LogInformation("Requesting new OAuth2 access token from SearchSG Search API using AccessKeyId: {AccessKeyId}", accessKeyId);

            try
            {
                // Create Basic Authentication credentials with Access Keys
                var credentialsString = $"{accessKeyId}:{accessKeySecret}";
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentialsString));

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/search/v1/oauth2/api/token");
                
                // Set Basic Auth header using AuthenticationHeaderValue
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                request.Headers.Accept.ParseAdd("application/json");
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36");

                // OAuth2 token endpoint requires grant_type in body
                var tokenRequestBody = JsonSerializer.Serialize(new { grant_type = "client_credentials" });
                request.Content = new StringContent(tokenRequestBody, Encoding.UTF8, "application/json");

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get Search API OAuth2 access token. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, responseContent);
                    throw new HttpRequestException($"Search API authentication failed: {response.StatusCode}. Response: {responseContent}");
                }

                // Parse token response (OAuth2 response may use access_token or accessToken)
                string? searchAccessToken = null;
                try
                {
                    using var tokenDoc = JsonDocument.Parse(responseContent);
                    if (tokenDoc.RootElement.TryGetProperty("access_token", out var accessTokenElement))
                    {
                        searchAccessToken = accessTokenElement.GetString();
                    }
                    else if (tokenDoc.RootElement.TryGetProperty("accessToken", out var accessTokenElementCamel))
                    {
                        searchAccessToken = accessTokenElementCamel.GetString();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse Search API OAuth2 token response");
                    throw new InvalidOperationException($"Failed to parse token response: {responseContent}", ex);
                }

                if (string.IsNullOrEmpty(searchAccessToken))
                {
                    _logger.LogError("No access token received from Search API OAuth2 endpoint. Response: {Response}", responseContent);
                    throw new InvalidOperationException("No access token received from Search API OAuth2 endpoint");
                }

                // Cache token with expiry (OAuth2 tokens typically expire in 20-30 minutes,
                // we set 18 minutes to refresh before expiry)
                var expiry = DateTime.UtcNow.AddMinutes(18);
                lock (_cacheLock)
                {
                    _cachedSearchApiTokens[accessKeyId] = (searchAccessToken, expiry);
                }

                _logger.LogInformation("Successfully obtained Search API OAuth2 access token, cached until {Expiry} (token length: {Length})", 
                    expiry, searchAccessToken.Length);
                return searchAccessToken;
            }
            catch (HttpRequestException)
            {
                // Re-throw HttpRequestException as-is (already logged)
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obtaining Search API OAuth2 access token");
                throw;
            }
        }

        /// <summary>
        /// Clear cached Search API token for a specific AccessKeyId
        /// </summary>
        public void ClearSearchApiToken(string? accessKeyId = null)
        {
            lock (_cacheLock)
            {
                if (string.IsNullOrEmpty(accessKeyId))
                {
                    _cachedSearchApiTokens.Clear();
                    _logger.LogInformation("All Search API access token cache cleared");
                }
                else
                {
                    _cachedSearchApiTokens.Remove(accessKeyId);
                    _logger.LogInformation("Search API access token cache cleared for AccessKeyId: {AccessKeyId}", accessKeyId);
                }
            }
        }
    }
}
