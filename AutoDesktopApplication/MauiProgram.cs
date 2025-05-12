using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Diagnostics;
using System.IO; // Required for File.Exists and File.Delete
using AutoDesktopApplication.Models;
using AutoDesktopApplication.Services;
using AutoDesktopApplication.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AutoDesktopApplication;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register database context
        builder.Services.AddDbContext<AppDbContext>((services, options) =>
        {
            string dbPath = AppDbContext.GetDatabasePath();
            options.UseSqlite($"Data Source={dbPath}");
            // Enable detailed logging in debug mode
            #if DEBUG
            options.EnableSensitiveDataLogging();
            options.LogTo(message => Debug.WriteLine(message));
            #endif
        }, ServiceLifetime.Singleton);

        // Register services
        builder.Services.AddSingleton<KeyboardHookService>();
        builder.Services.AddSingleton<InputLogService>();
        builder.Services.AddSingleton<InputRecordingService>();
        builder.Services.AddSingleton<InputPlaybackService>();
        builder.Services.AddSingleton<ScreenCaptureService>();
        builder.Services.AddSingleton<VisionApiService>();
        builder.Services.AddSingleton<IScreenshot>(Screenshot.Default); // Restore IScreenshot registration
        
        // Register HttpClient for Ollama service with a base address and timeout
        builder.Services.AddHttpClient("Ollama", client =>
        {
            client.BaseAddress = new Uri("http://localhost:11434/api/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddSingleton<OllamaService>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Ollama");
            var logger = serviceProvider.GetRequiredService<ILogger<OllamaService>>();
            return new OllamaService(httpClient, logger);
        });

        // Register ViewModels
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<ProjectsViewModel>();
        builder.Services.AddTransient<WorkflowsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Register pages
        builder.Services.AddTransient<MainPage>();
        
        // Template version of BlazorWebView
        builder.Services.AddMauiBlazorWebView();
        
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Create and initialize the database
        var app = builder.Build();
        
        InitializeDatabase(app);

        return app; // Return the already built app
    }

    private static void InitializeDatabase(MauiApp app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetService<ILogger<MauiApp>>();

        string dbPath = AppDbContext.GetDatabasePath(); 
        logger?.LogInformation($"[MauiProgram.InitializeDatabase] Database path determined to be: {dbPath}");

        try
        {
            logger?.LogInformation($"[MauiProgram.InitializeDatabase] Checking for existing database file at: {dbPath}");
            // if (File.Exists(dbPath))
            // {
            //     logger?.LogWarning($"[MauiProgram.InitializeDatabase] Existing database file found at {dbPath}. Attempting to delete it.");
            //     File.Delete(dbPath);
            //     logger?.LogInformation($"[MauiProgram.InitializeDatabase] Successfully deleted existing database file at: {dbPath}");
            // }
            // else
            // {
            //     logger?.LogInformation($"[MauiProgram.InitializeDatabase] No existing database file found at: {dbPath}. Proceeding to migrate.");
            // }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, $"[MauiProgram.InitializeDatabase] FAILED to delete existing database file at: {dbPath}. This might cause 'table already exists' errors if migrations run on an old DB. Manual deletion of this file might be required if the error persists.");
            // We will allow the process to continue to Migrate() to see if the original error still occurs.
            // If it does, it confirms the delete failed and the old DB is still problematic.
        }
        
        try
        {
            logger?.LogInformation($"[MauiProgram.InitializeDatabase] Attempting to migrate database at: {dbPath}");
            dbContext.Database.Migrate();
            logger?.LogInformation($"Database initialized and migrations applied successfully at path: {dbPath}");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, $"An error occurred while initializing/migrating the database at path: {dbPath}");
            throw; 
        }
    }
}
