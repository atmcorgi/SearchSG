using SearchSGTestApp.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SearchSGTestApp.Services
{
    public class SearchSGAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly SearchSGConfiguration _config;
        private readonly ILogger<SearchSGAuthService> _logger;
        private string? _cachedToken;
        private DateTime _tokenExpiry;

        public SearchSGAuthService(HttpClient httpClient, SearchSGConfiguration config, ILogger<SearchSGAuthService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
            {
                _logger.LogInformation("Using cached access token");
                return _cachedToken;
            }

            _logger.LogInformation("Requesting new access token from SearchSG");

            try
            {
                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/admin/v1/auth/token");
                request.Headers.Add("Authorization", $"Basic {credentials}");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                // Add empty content as shown in documentation
                request.Content = new StringContent("", Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get access token. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Authentication failed: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tokenResponse?.AccessToken == null)
                {
                    throw new InvalidOperationException("No access token received from authentication endpoint");
                }

                _cachedToken = tokenResponse.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddMinutes(55); // Refresh 5 minutes before expiry

                _logger.LogInformation("Successfully obtained access token, expires at {Expiry}", _tokenExpiry);
                return _cachedToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obtaining access token");
                throw;
            }
        }

        public void ClearToken()
        {
            _cachedToken = null;
            _tokenExpiry = DateTime.MinValue;
            _logger.LogInformation("Access token cache cleared");
        }
    }
}
