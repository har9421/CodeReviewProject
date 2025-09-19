using CodeReviewRunner.Configuration;
using CodeReviewRunner.Interfaces;
using CodeReviewRunner.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace CodeReviewRunner;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/codereview-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Starting CodeReviewRunner Enterprise v2.0");

            var host = CreateHostBuilder(args).Build();
            var app = host.Services.GetRequiredService<CodeReviewApplication>();

            return await app.RunAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<CodeReviewOptions>(
                    context.Configuration.GetSection(CodeReviewOptions.SectionName));
                services.Configure<ResilienceOptions>(
                    context.Configuration.GetSection(ResilienceOptions.SectionName));

                // HTTP Client
                services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>();

                // Services
                services.AddScoped<ICodeReviewService, CodeReviewService>();
                services.AddScoped<IAnalysisService, AnalysisService>();
                services.AddScoped<IRulesService, RulesService>();

                // Caching
                services.AddMemoryCache();

                // Health Checks removed (unused)

                // Application
                services.AddScoped<CodeReviewApplication>();

                // Validation
                // services.AddFluentValidationAutoValidation();
            });

}