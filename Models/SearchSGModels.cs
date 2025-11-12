namespace SearchSGTestApp.Models
{
    public class SearchSGConfiguration
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string ApplicationId { get; set; } = string.Empty;
    }

    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
    }

    public class PushDocumentsRequest
    {
        public List<DocumentToAdd> DocumentsToAdd { get; set; } = new();
        public List<string> DocumentsToDelete { get; set; } = new();
    }
    public class DocumentToAdd
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime? LastUpdated { get; set; }
        public string? ContentType { get; set; }
        public string[]? Categories { get; set; }
        public string[]? Keywords { get; set; }
        public string[]? CustomFilter1 { get; set; } = new string[0];
        public string[]? CustomFilter2 { get; set; } = new string[0];
        public int? SearchRankingScore { get; set; }
    }

    public class PushJobResponse
    {
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class FailedDocumentToDelete
    {
        [System.Text.Json.Serialization.JsonPropertyName("documentId")]
        public string? DocumentId { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public string? Error { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    public class JobStatusResponse
    {
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<DocumentToAdd> FailedDocumentsToAdd { get; set; } = new();
        public List<FailedDocumentToDelete> FailedDocumentsToDelete { get; set; } = new();
    }

    public class TestDocumentViewModel
    {
        public string DocumentId { get; set; } = $"doc-{Guid.NewGuid():N}";
        public string Url { get; set; } = "https://example.com/test-page";
        public string Title { get; set; } = "Test Document";
        public string Content { get; set; } = "This is a test document for SearchSG Push API testing.";
        public string ContentType { get; set; } = "Pages";
        public string Categories { get; set; } = "";
        public string Keywords { get; set; } = "test,searchsg,api";
        public string CustomFilter1 { get; set; } = "test-tag";
        public int SearchRankingScore { get; set; } = 100;
    }

    public class SearchQueryRequest
    {
        public string Query { get; set; } = "*"; // Default to match all
        public int Size { get; set; } = 20; // Number of results
        public int From { get; set; } = 0; // Offset for pagination
        public string? Scope { get; set; } // Optional scope (e.g., "domain")
    }

    public class ApiTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? JobId { get; set; }
        public string? ErrorDetails { get; set; }
        public string? ItemsList { get; set; } // HTML formatted list of search items
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class AccessKeyData
    {
        public string AccessKeyId { get; set; } = string.Empty;
        public string AccessKeySecret { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AppConfigTemp
    {
        public SearchSGConfigTemp? SearchSG { get; set; }
        public Dictionary<string, object>? AccessKeys { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    public class SearchSGConfigTemp
    {
        public string? BaseUrl { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? ApplicationId { get; set; }
    }
}
