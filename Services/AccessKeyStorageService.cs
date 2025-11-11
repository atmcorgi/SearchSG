using System.Text.Json;
using SearchSGTestApp.Models;

namespace SearchSGTestApp.Services
{
    public class AccessKeyStorageService
    {
        private readonly string _configFilePath;
        private readonly ILogger<AccessKeyStorageService> _logger;

        public AccessKeyStorageService(ILogger<AccessKeyStorageService> logger)
        {
            _logger = logger;
            _configFilePath = Path.Combine(Directory.GetCurrentDirectory(), "appconfigs.json");
        }

        public async Task SaveAccessKeysAsync(string applicationId, object accessKeysData)
        {
            try
            {
                var config = await LoadConfigAsync();
                
                if (config.AccessKeys == null)
                    config.AccessKeys = new Dictionary<string, object>();

                // Handle both single key object and API response with data array
                if (accessKeysData is JsonElement jsonElement)
                {
                    if (jsonElement.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                    {
                        // API response format: { "data": [...] }
                        var firstKey = dataArray.EnumerateArray().FirstOrDefault();
                        if (firstKey.ValueKind != JsonValueKind.Undefined)
                        {
                            config.AccessKeys[applicationId] = JsonSerializer.Deserialize<object>(firstKey.GetRawText());
                        }
                    }
                    else
                    {
                        // Direct key object
                        config.AccessKeys[applicationId] = JsonSerializer.Deserialize<object>(jsonElement.GetRawText());
                    }
                }
                else
                {
                    config.AccessKeys[applicationId] = accessKeysData;
                }

                config.LastUpdated = DateTime.UtcNow;

                var jsonString = JsonSerializer.Serialize(config, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(_configFilePath, jsonString);
                _logger.LogInformation($"Access keys saved for application {applicationId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save access keys for application {applicationId}");
                throw;
            }
        }

        public async Task<object?> GetAccessKeysAsync(string applicationId)
        {
            try
            {
                var config = await LoadConfigAsync();
                
                if (config.AccessKeys?.ContainsKey(applicationId) == true)
                {
                    return config.AccessKeys[applicationId];
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load access keys for application {applicationId}");
                return null;
            }
        }

        /// <summary>
        /// Parse Access Keys from various formats (object, array, JsonElement) and return AccessKeyData
        /// This method handles all possible formats that Access Keys can be stored in
        /// </summary>
        public AccessKeyData? ParseAccessKeyData(object? accessKeysData)
        {
            if (accessKeysData == null)
                return null;

            try
            {
                // Always serialize to JSON string first to avoid issues with disposed JsonDocument
                // This ensures we have a standalone copy of the data
                string jsonString;
                
                if (accessKeysData is JsonElement element)
                {
                    // JsonElement might be from a disposed JsonDocument, so clone it by serializing
                    jsonString = element.GetRawText();
                }
                else if (accessKeysData is string str)
                {
                    // Already a string, use it directly
                    jsonString = str;
                }
                else
                {
                    // Serialize object to JSON string
                    jsonString = JsonSerializer.Serialize(accessKeysData);
                }

                // Parse the JSON string into a new JsonDocument
                using var doc = JsonDocument.Parse(jsonString);
                var jsonElement = doc.RootElement;

                // Handle different JSON structures
                return ParseAccessKeyFromJsonElement(jsonElement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Access Key data: {Error}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Parse AccessKeyData from JsonElement, handling object, array, and nested formats
        /// Note: This method extracts values immediately to avoid issues with disposed JsonDocument
        /// </summary>
        private AccessKeyData? ParseAccessKeyFromJsonElement(JsonElement element)
        {
            try
            {
                JsonElement targetElement;

                // Case 1: Element is an array - take first item
                if (element.ValueKind == JsonValueKind.Array)
                {
                    if (element.GetArrayLength() == 0)
                    {
                        _logger.LogWarning("Access Keys array is empty");
                        return null;
                    }
                    targetElement = element[0];
                }
                // Case 2: Element is an object with "data" property containing array
                else if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("data", out var dataProperty))
                    {
                        // Has "data" property
                        if (dataProperty.ValueKind == JsonValueKind.Array)
                        {
                            if (dataProperty.GetArrayLength() == 0)
                            {
                                _logger.LogWarning("Access Keys data array is empty");
                                return null;
                            }
                            targetElement = dataProperty[0];
                        }
                        else if (dataProperty.ValueKind == JsonValueKind.Object)
                        {
                            // "data" is an object, use it directly
                            targetElement = dataProperty;
                        }
                        else
                        {
                            _logger.LogWarning("Unexpected 'data' property type: {ValueKind}", dataProperty.ValueKind);
                            return null;
                        }
                    }
                    else
                    {
                        // No "data" property, check if it's already an access key object
                        // Check if it has accessKeyId property
                        if (element.TryGetProperty("accessKeyId", out _))
                        {
                            // This is already an access key object
                            targetElement = element;
                        }
                        else
                        {
                            _logger.LogWarning("Object does not contain 'data' property or 'accessKeyId' property");
                            return null;
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Unexpected Access Key format: {ValueKind}", element.ValueKind);
                    return null;
                }

                // Extract all values immediately (before JsonDocument is disposed)
                var accessKeyData = new AccessKeyData();
                string? accessKeyId = null;
                string? accessKeySecret = null;
                string? description = null;
                DateTime? createdAt = null;

                // Extract accessKeyId
                if (targetElement.TryGetProperty("accessKeyId", out var idElement))
                {
                    accessKeyId = idElement.GetString();
                }

                // Extract accessKeySecret
                if (targetElement.TryGetProperty("accessKeySecret", out var secretElement))
                {
                    accessKeySecret = secretElement.GetString();
                    // Debug: Log secret details to verify parsing
                    _logger.LogInformation("Parsed accessKeySecret: length={Length}, valueKind={ValueKind}", 
                        accessKeySecret?.Length ?? 0, secretElement.ValueKind);
                }

                // Extract description (optional)
                if (targetElement.TryGetProperty("description", out var descElement))
                {
                    description = descElement.GetString();
                }

                // Extract createdAt (optional)
                if (targetElement.TryGetProperty("createdAt", out var createdAtElement))
                {
                    if (createdAtElement.ValueKind == JsonValueKind.String)
                    {
                        var dateStr = createdAtElement.GetString();
                        if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var dateTime))
                        {
                            createdAt = dateTime;
                        }
                    }
                }

                // Assign extracted values
                accessKeyData.AccessKeyId = accessKeyId ?? string.Empty;
                accessKeyData.AccessKeySecret = accessKeySecret ?? string.Empty;
                accessKeyData.Description = description ?? string.Empty;
                if (createdAt.HasValue)
                {
                    accessKeyData.CreatedAt = createdAt.Value;
                }

                // Validate that we have at least accessKeyId and accessKeySecret
                if (string.IsNullOrEmpty(accessKeyData.AccessKeyId) || 
                    string.IsNullOrEmpty(accessKeyData.AccessKeySecret))
                {
                    _logger.LogWarning("Access Key data is incomplete. Missing accessKeyId or accessKeySecret. " +
                                     $"HasId: {!string.IsNullOrEmpty(accessKeyId)}, HasSecret: {!string.IsNullOrEmpty(accessKeySecret)}");
                    return null;
                }

                return accessKeyData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Access Key from JsonElement: {Error}", ex.Message);
                return null;
            }
        }

        public async Task<SearchSGConfiguration?> GetSearchSGConfigAsync()
        {
            try
            {
                var config = await LoadConfigAsync();
                
                if (config.SearchSG != null)
                {
                    return new SearchSGConfiguration
                    {
                        BaseUrl = config.SearchSG.BaseUrl ?? "https://api.services.search.gov.sg",
                        ClientId = config.SearchSG.ClientId ?? "",
                        ClientSecret = config.SearchSG.ClientSecret ?? "",
                        ApplicationId = config.SearchSG.ApplicationId ?? ""
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load SearchSG configuration");
                return null;
            }
        }

        private async Task<AppConfig> LoadConfigAsync()
        {
            if (!File.Exists(_configFilePath))
            {
                return new AppConfig();
            }

            try
            {
                var jsonString = await File.ReadAllTextAsync(_configFilePath);
                return JsonSerializer.Deserialize<AppConfig>(jsonString, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load config file, creating new one");
                return new AppConfig();
            }
        }

        public class AppConfig
        {
            public SearchSGConfigData? SearchSG { get; set; }
            public Dictionary<string, object>? AccessKeys { get; set; }
            public DateTime? LastUpdated { get; set; }
        }

        public class SearchSGConfigData
        {
            public string? BaseUrl { get; set; }
            public string? ClientId { get; set; }
            public string? ClientSecret { get; set; }
            public string? ApplicationId { get; set; }
        }
    }
}
