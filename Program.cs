using SearchSGTestApp.Models;
using SearchSGTestApp.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure SearchSG settings - load synchronously for now
SearchSGConfiguration searchSGConfig;
try 
{
    var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appconfigs.json");
    if (File.Exists(configPath))
    {
        var jsonString = File.ReadAllText(configPath);
        var appConfig = JsonSerializer.Deserialize<AppConfigTemp>(jsonString, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        if (appConfig?.SearchSG != null)
        {
            searchSGConfig = new SearchSGConfiguration
            {
                BaseUrl = appConfig.SearchSG.BaseUrl ?? "https://api.services.search.gov.sg",
                ClientId = appConfig.SearchSG.ClientId ?? "",
                ClientSecret = appConfig.SearchSG.ClientSecret ?? "",
                ApplicationId = appConfig.SearchSG.ApplicationId ?? ""
            };
            Console.WriteLine($"Loaded SearchSG config from appconfigs.json - ApplicationId: {searchSGConfig.ApplicationId}");
        }
        else
        {
            throw new Exception("SearchSG section not found in appconfigs.json");
        }
    }
    else
    {
        throw new FileNotFoundException("appconfigs.json not found");
    }
}
catch (Exception ex)
{
    // Fallback to default config if file doesn't exist or has issues
    searchSGConfig = new SearchSGConfiguration
    {
        BaseUrl = "https://api.services.search.gov.sg",
        ApplicationId = "b306a35b-f61b-403c-83ea-acfb499cf3c5"
    };
    Console.WriteLine($"Warning: Using default SearchSG configuration. Error: {ex.Message}");
}

// Register SearchSG configuration as singleton
builder.Services.AddSingleton(searchSGConfig);

// Add HTTP client
builder.Services.AddHttpClient();

// Register services with proper dependencies
builder.Services.AddScoped<SearchSGAuthService>(provider =>
{
    var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient();
    var config = provider.GetRequiredService<SearchSGConfiguration>();
    var logger = provider.GetRequiredService<ILogger<SearchSGAuthService>>();
    return new SearchSGAuthService(httpClient, config, logger);
});

builder.Services.AddScoped<SearchSGPushService>(provider =>
{
    var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient();
    var authService = provider.GetRequiredService<SearchSGAuthService>();
    var config = provider.GetRequiredService<SearchSGConfiguration>();
    var logger = provider.GetRequiredService<ILogger<SearchSGPushService>>();
    return new SearchSGPushService(httpClient, authService, config, logger);
});

builder.Services.AddScoped<AccessKeyStorageService>();

builder.Services.AddScoped<SearchSGAdminService>(provider =>
{
    var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient();
    var authService = provider.GetRequiredService<SearchSGAuthService>();
    return new SearchSGAdminService(httpClient, authService);
});

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
