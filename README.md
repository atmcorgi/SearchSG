# SearchSG Test Application

A simple C# ASP.NET Core web application to test SearchSG Admin Operations API flows including authentication, document push, and job monitoring.

## Features

- **Authentication Testing**: Test OAuth2 flow to obtain access tokens
- **Document Push**: Push individual documents to SearchSG index
- **Batch Operations**: Push multiple documents or delete documents
- **Job Monitoring**: Check the status of indexing jobs
- **Real-time Feedback**: Wait for job completion with status updates
- **User-friendly Interface**: Bootstrap-based web UI for easy testing

## Setup Instructions

### 1. Prerequisites
- .NET 8.0 SDK
- SearchSG Admin Portal access with:
  - Client ID and Client Secret
  - Application ID with Push API datasource enabled

### 2. Configuration
Update the `appsettings.json` file with your SearchSG credentials:

```json
{
  "SearchSG": {
    "BaseUrl": "https://api.services.search.gov.sg",
    "ClientId": "your-client-id-here",
    "ClientSecret": "your-client-secret-here",
    "ApplicationId": "your-application-id-here"
  }
}
```

### 3. Run the Application
```bash
cd SearchSGTestApp
dotnet restore
dotnet run
```

The application will be available at `https://localhost:5001` or `http://localhost:5000`.

## Usage

### Authentication Test
- Click "Test Authentication" to verify your credentials
- This will obtain and cache an access token for subsequent API calls

### Quick Push Test
- Click "Quick Push Document" to push a sample document with default values
- Useful for quick testing without filling out forms

### Custom Document Push
- Fill out the form with your document details
- Click "Push Document" to queue the document for indexing
- Click "Push & Wait for Completion" to push and monitor until completion

### Job Status Monitoring
- Enter a Job ID (returned from push operations) to check its status
- Shows job completion status and any failed documents

### Document Deletion
- Enter a Document ID to remove it from the search index
- Returns a job ID for monitoring the deletion process

## API Endpoints Tested

- `POST /admin/v1/auth/token` - Authentication
- `POST /admin/v1/bootstrap/applications/{appId}/documents` - Push documents
- `GET /admin/v1/bootstrap/indexingJobs/{jobId}` - Job status

## Document Fields

### Required Fields
- **Document ID**: Unique identifier for the document
- **URL**: Document URL
- **Title**: Document title
- **Content**: Document content

### Optional Fields
- **Content Type**: Information, Service, News, Event
- **Categories**: Comma-separated list of categories
- **Keywords**: Comma-separated list of keywords
- **Custom Filter 1**: Custom tags for filtering
- **Search Ranking Score**: 1-1000 (higher = more relevant)

## Error Handling

The application includes comprehensive error handling:
- Authentication token caching and refresh
- HTTP error status handling
- Detailed error messages in the UI
- Logging to console and debug output

## Integration with Sitecore

This test application demonstrates the patterns you can use to integrate SearchSG with your Sitecore backend:

1. **Event-driven Updates**: Push documents when Sitecore items are saved
2. **Batch Processing**: Process multiple items during publish operations
3. **Real-time Indexing**: Immediate search availability for critical content
4. **Error Recovery**: Handle failed documents and retry mechanisms

## Troubleshooting

### Common Issues

1. **Authentication Failed (401)**
   - Verify ClientId and ClientSecret in appsettings.json
   - Check if credentials are valid in SearchSG Admin Portal

2. **Application Not Found (404)**
   - Verify ApplicationId in appsettings.json
   - Ensure the application exists and has Push API enabled

3. **Push Documents Failed (400)**
   - Check document structure and required fields
   - Verify JSON serialization is correct

4. **Job Status Not Found**
   - Job IDs are case-sensitive
   - Jobs may expire after a certain time period

### Logging

Check the console output for detailed logging information including:
- Authentication requests and responses
- Push API calls and responses
- Job status checks
- Error details and stack traces
