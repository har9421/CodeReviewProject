using CodeReviewBot.Application.Interfaces;
using CodeReviewBot.Application.Services;
using CodeReviewBot.Domain.Interfaces;
using CodeReviewBot.Infrastructure.ExternalServices;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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

// Register Application Services
builder.Services.AddScoped<IPullRequestAnalysisService, PullRequestAnalysisService>();

// Register Infrastructure Services
builder.Services.AddScoped<IPullRequestRepository, AzureDevOpsService>();
builder.Services.AddScoped<ICodeAnalyzer, CodeAnalyzerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
