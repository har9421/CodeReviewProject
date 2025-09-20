using CodeReviewBot.Services;
using CodeReviewBot.Configuration;
using CodeReviewBot.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace CodeReviewBot;

public class Program
{
    public static void Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/codereview-bot-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Starting Code Review Bot v1.0");

            var builder = WebApplication.CreateBuilder(args);

            // Add Serilog
            builder.Host.UseSerilog();

            // Add services to the container
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Configuration
            builder.Services.Configure<BotOptions>(
                builder.Configuration.GetSection(BotOptions.SectionName));
            builder.Services.Configure<AzureDevOpsOptions>(
                builder.Configuration.GetSection(AzureDevOpsOptions.SectionName));
            builder.Services.Configure<AIOptions>(
                builder.Configuration.GetSection(AIOptions.SectionName));
            builder.Services.Configure<LearningOptions>(
                builder.Configuration.GetSection(LearningOptions.SectionName));

            // HTTP Client
            builder.Services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>();

            // Services
            builder.Services.AddScoped<IWebhookService, WebhookService>();
            builder.Services.AddScoped<ICodeReviewService, CodeReviewService>();
            builder.Services.AddScoped<IAnalysisService, AnalysisService>();
            builder.Services.AddScoped<IAIAnalysisService, AIAnalysisService>();
            builder.Services.AddScoped<ILearningService, LearningService>();
            builder.Services.AddScoped<IMetricsService, MetricsService>();

            // Caching
            builder.Services.AddMemoryCache();

            // Health Checks
            builder.Services.AddHealthChecks();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHealthChecks("/health");

            Log.Information("Code Review Bot is ready to receive webhooks");
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
