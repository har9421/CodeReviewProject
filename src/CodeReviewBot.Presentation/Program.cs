using CodeReviewBot.Application.Interfaces;
using CodeReviewBot.Application.Services;
using CodeReviewBot.Domain.Interfaces;
using CodeReviewBot.Shared.Configuration;
using CodeReviewBot.Infrastructure.ExternalServices;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on all interfaces
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5002); // HTTP - changed from 5000 to avoid AirPlay conflict
    options.ListenAnyIP(5003, listenOptions => // HTTPS - changed from 5001
    {
        listenOptions.UseHttps();
    });
});

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/codereviewbot-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register HttpClient
builder.Services.AddHttpClient();

// Configure options
builder.Services.Configure<BotOptions>(builder.Configuration.GetSection(BotOptions.SectionName));

// Register Application Services
builder.Services.AddScoped<IPullRequestAnalysisService, IntelligentPullRequestAnalysisService>();

// Register Infrastructure Services
builder.Services.AddScoped<IPullRequestRepository, AzureDevOpsService>();
builder.Services.AddScoped<ICodeAnalyzer, IntelligentCodeAnalyzerService>();
builder.Services.AddScoped<ILearningService, LearningService>();
builder.Services.AddScoped<IPerformanceMonitoringService, PerformanceMonitoringService>();
builder.Services.AddScoped<GitHubDataIngestionService>();
builder.Services.AddScoped<BatchProcessingService>();
builder.Services.AddScoped<DataPreprocessingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure forwarded headers for proxy scenarios (like ngrok)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// Only use HTTPS redirection if not running behind a proxy
// When using ngrok, disable HTTPS redirection since ngrok handles HTTPS termination
var isBehindProxy = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NGROK_URL"));

if (!isBehindProxy)
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
