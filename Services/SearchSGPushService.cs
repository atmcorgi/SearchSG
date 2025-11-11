using SearchSGTestApp.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SearchSGTestApp.Services
{
    public class SearchSGPushService
    {
        private readonly HttpClient _httpClient;
        private readonly SearchSGAuthService _authService;
        private readonly SearchSGConfiguration _config;
        private readonly ILogger<SearchSGPushService> _logger;

        public SearchSGPushService(
            HttpClient httpClient,
            SearchSGAuthService authService,
            SearchSGConfiguration config,
            ILogger<SearchSGPushService> logger)
        {
            _httpClient = httpClient;
            _authService = authService;
            _config = config;
            _logger = logger;
        }

        public async Task<PushJobResponse> PushDocumentsAsync(PushDocumentsRequest request)
        {
            _logger.LogInformation("Pushing {AddCount} documents to add and {DeleteCount} documents to delete",
                request.DocumentsToAdd.Count, request.DocumentsToDelete.Count);

            try
            {
                var token = await _authService.GetAccessTokenAsync();
                _logger.LogDebug("Using access token: {Token}", token?.Substring(0, Math.Min(20, token?.Length ?? 0)) + "...");

                var url = $"{_config.BaseUrl}/admin/v1/bootstrap/applications/{_config.ApplicationId}/documents";
                _logger.LogDebug("Push URL: {Url}", url);

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

                httpRequest.Headers.Add("Authorization", $"Bearer {token}");
                httpRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                httpRequest.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                _logger.LogDebug("Push request payload: {Payload}", jsonContent);
                _logger.LogDebug("Request headers: Authorization=Bearer {TokenPrefix}..., User-Agent={UserAgent}",
                    token?.Substring(0, Math.Min(10, token?.Length ?? 0)),
                    httpRequest.Headers.GetValues("User-Agent").FirstOrDefault());

                var response = await _httpClient.SendAsync(httpRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Push documents failed. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, errorContent);

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _authService.ClearToken();
                    }

                    throw new HttpRequestException($"Push documents failed: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jobResponse = JsonSerializer.Deserialize<PushJobResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (jobResponse == null)
                {
                    throw new InvalidOperationException("Invalid response from push documents endpoint");
                }

                _logger.LogInformation("Successfully pushed documents. JobId: {JobId}, Status: {Status}",
                    jobResponse.JobId, jobResponse.Status);

                return jobResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing documents to SearchSG");
                throw;
            }
        }

        public async Task<JobStatusResponse> GetJobStatusAsync(string jobId)
        {
            _logger.LogInformation("Getting job status for JobId: {JobId}", jobId);

            try
            {
                var token = await _authService.GetAccessTokenAsync();

                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{_config.BaseUrl}/admin/v1/bootstrap/indexingJobs/{jobId}");

                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) postman-docs");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Get job status failed. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, errorContent);

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _authService.ClearToken();
                    }

                    throw new HttpRequestException($"Get job status failed: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Job status raw response: {Response}", responseContent);

                // Parse response manually to handle failedDocumentsToDelete which can be string or object
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                var jobStatus = new JobStatusResponse();

                if (root.TryGetProperty("jobId", out var jobIdElement))
                    jobStatus.JobId = jobIdElement.GetString() ?? string.Empty;
                
                if (root.TryGetProperty("status", out var statusElement))
                    jobStatus.Status = statusElement.GetString() ?? string.Empty;
                
                if (root.TryGetProperty("createdAt", out var createdAtElement))
                    jobStatus.CreatedAt = createdAtElement.GetDateTime();
                
                if (root.TryGetProperty("updatedAt", out var updatedAtElement))
                    jobStatus.UpdatedAt = updatedAtElement.GetDateTime();

                // Parse failedDocumentsToAdd
                if (root.TryGetProperty("failedDocumentsToAdd", out var failedAddElement) && failedAddElement.ValueKind == JsonValueKind.Array)
                {
                    jobStatus.FailedDocumentsToAdd = JsonSerializer.Deserialize<List<DocumentToAdd>>(failedAddElement.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<DocumentToAdd>();
                }

                // Parse failedDocumentsToDelete - can be array of strings or objects
                if (root.TryGetProperty("failedDocumentsToDelete", out var failedDeleteElement) && failedDeleteElement.ValueKind == JsonValueKind.Array)
                {
                    jobStatus.FailedDocumentsToDelete = new List<FailedDocumentToDelete>();
                    foreach (var item in failedDeleteElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            // If it's a string, treat it as documentId
                            jobStatus.FailedDocumentsToDelete.Add(new FailedDocumentToDelete
                            {
                                DocumentId = item.GetString()
                            });
                        }
                        else if (item.ValueKind == JsonValueKind.Object)
                        {
                            // If it's an object, parse it - try different property names
                            var failedDoc = new FailedDocumentToDelete();
                            
                            // Try different possible property names for documentId
                            if (item.TryGetProperty("documentId", out var docIdElement))
                                failedDoc.DocumentId = docIdElement.GetString();
                            else if (item.TryGetProperty("document_id", out var docIdElement2))
                                failedDoc.DocumentId = docIdElement2.GetString();
                            else if (item.TryGetProperty("id", out var idElement))
                                failedDoc.DocumentId = idElement.GetString();
                            
                            // Try different possible property names for error
                            if (item.TryGetProperty("error", out var errorElement))
                                failedDoc.Error = errorElement.GetString();
                            else if (item.TryGetProperty("errorMessage", out var errorMsgElement))
                                failedDoc.Error = errorMsgElement.GetString();
                            else if (item.TryGetProperty("reason", out var reasonElement))
                                failedDoc.Error = reasonElement.GetString();
                            
                            // Try different possible property names for message
                            if (item.TryGetProperty("message", out var messageElement))
                                failedDoc.Message = messageElement.GetString();
                            
                            // If documentId is still null, try to get it from the object itself
                            if (string.IsNullOrEmpty(failedDoc.DocumentId))
                            {
                                // Log the raw JSON for debugging
                                _logger.LogWarning("Failed document object does not have documentId: {Json}", item.GetRawText());
                            }
                            
                            jobStatus.FailedDocumentsToDelete.Add(failedDoc);
                        }
                    }
                }

                _logger.LogInformation("Job status retrieved. JobId: {JobId}, Status: {Status}",
                    jobStatus.JobId, jobStatus.Status);

                return jobStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting job status from SearchSG");
                throw;
            }
        }

        public async Task<bool> WaitForJobCompletionAsync(string jobId, int maxWaitTimeMinutes = 5)
        {
            _logger.LogInformation("Waiting for job completion. JobId: {JobId}, MaxWait: {MaxWait} minutes",
                jobId, maxWaitTimeMinutes);

            var startTime = DateTime.UtcNow;
            var maxWaitTime = TimeSpan.FromMinutes(maxWaitTimeMinutes);

            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                try
                {
                    var status = await GetJobStatusAsync(jobId);

                    switch (status.Status.ToUpper())
                    {
                        case "COMPLETED":
                            _logger.LogInformation("Job completed successfully. JobId: {JobId}", jobId);
                            return true;
                        case "ERROR":
                            _logger.LogError("Job failed. JobId: {JobId}", jobId);
                            return false;
                        case "PENDING":
                            _logger.LogDebug("Job still pending. JobId: {JobId}", jobId);
                            break;
                        default:
                            _logger.LogDebug("Job status: {Status}. JobId: {JobId}", status.Status, jobId);
                            break;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5)); // Wait 5 seconds before checking again
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking job status, will retry. JobId: {JobId}", jobId);
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }

            _logger.LogWarning("Job did not complete within {MaxWait} minutes. JobId: {JobId}", maxWaitTimeMinutes, jobId);
            return false;
        }
    }
}
