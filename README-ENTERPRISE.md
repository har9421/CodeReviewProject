# CodeReviewRunner Enterprise v2.0

A comprehensive, enterprise-grade code review automation tool designed for large-scale development teams and organizations.

## 🏢 Enterprise Features

### Architecture & Design Patterns

- **Dependency Injection** - Full IoC container with Microsoft.Extensions.DependencyInjection
- **Configuration Management** - Hierarchical configuration with environment-specific settings
- **Structured Logging** - Serilog with multiple sinks (Console, File, Application Insights)
- **Health Checks** - Built-in health monitoring for Azure DevOps connectivity
- **Resilience Patterns** - Polly integration for retry policies and circuit breakers
- **Caching** - In-memory caching for rules and API responses
- **Validation** - FluentValidation for input validation

### Monitoring & Observability

- **Application Insights** - Full telemetry and monitoring integration
- **Structured Logging** - JSON-formatted logs with correlation IDs
- **Performance Metrics** - Detailed timing and performance tracking
- **Error Tracking** - Comprehensive error handling and reporting
- **Health Endpoints** - Built-in health check endpoints

### Scalability & Performance

- **Async/Await** - Full asynchronous programming model
- **Concurrent Processing** - Configurable parallel file analysis
- **Memory Management** - Efficient memory usage with proper disposal
- **Caching Strategy** - Intelligent caching to reduce API calls
- **Resource Pooling** - HTTP client pooling and connection management

## 🚀 Quick Start

### Prerequisites

- .NET 8.0 SDK
- Node.js (for JavaScript/TypeScript analysis)
- Azure DevOps Personal Access Token (for production)

### Installation

```bash
# Clone the repository
git clone <repository-url>
cd CodeReviewProject

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

### Configuration

The application uses a hierarchical configuration system:

1. **appsettings.json** - Base configuration
2. **appsettings.Development.json** - Development overrides
3. **appsettings.Production.json** - Production overrides
4. **Environment Variables** - Runtime overrides
5. **Command Line Arguments** - Execution-time overrides

#### Key Configuration Sections

```json
{
  "CodeReview": {
    "AzureDevOps": {
      "BaseUrl": "https://dev.azure.com",
      "ApiVersion": "7.0",
      "TimeoutSeconds": 30,
      "RetryAttempts": 3
    },
    "Analysis": {
      "MaxConcurrentFiles": 10,
      "CacheRulesMinutes": 60,
      "SupportedFileExtensions": [".cs", ".js", ".jsx", ".ts", ".tsx"]
    },
    "Notifications": {
      "EnableComments": true,
      "EnableSummary": true,
      "MaxCommentsPerFile": 50
    }
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": ["Console", "File", "ApplicationInsights"]
  }
}
```

## 🔧 Usage

### Production Mode

```bash
# Set your Azure DevOps PAT
export SYSTEM_ACCESSTOKEN="your-pat-token"

# Run analysis on a pull request
dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj \
  "/path/to/repo" \
  "https://your-org.com/coding-standards.json" \
  "123" \
  "https://dev.azure.com/yourorg" \
  "yourproject" \
  "repo-guid"
```

### Test Mode

```bash
# Run with sample data (no Azure DevOps required)
dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj \
  test \
  "https://your-org.com/coding-standards.json" \
  test \
  "https://dev.azure.com/yourorg" \
  "yourproject" \
  "test-repo-id"
```

### Docker Support

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/CodeReviewRunner/CodeReviewRunner.csproj", "src/CodeReviewRunner/"]
RUN dotnet restore "src/CodeReviewRunner/CodeReviewRunner.csproj"
COPY . .
WORKDIR "/src/src/CodeReviewRunner"
RUN dotnet build "CodeReviewRunner.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CodeReviewRunner.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CodeReviewRunner.dll"]
```

## 📊 Monitoring & Logging

### Application Insights Integration

```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "your-instrumentation-key",
    "EnableAdaptiveSampling": true,
    "EnableQuickPulseMetricStream": true
  }
}
```

### Log Levels

- **Development**: Debug level with detailed information
- **Production**: Warning level with essential information only
- **Staging**: Information level with balanced detail

### Log Sinks

- **Console** - Real-time development feedback
- **File** - Persistent log storage with rotation
- **Application Insights** - Cloud-based monitoring and analytics

## 🏗️ Architecture

### Service Layer

- **ICodeReviewService** - Main orchestration service
- **IAzureDevOpsService** - Azure DevOps API integration
- **IAnalysisService** - Code analysis coordination
- **IRulesService** - Rules management and caching

### Configuration Layer

- **CodeReviewOptions** - Main application configuration
- **ResilienceOptions** - Retry and circuit breaker settings
- **Environment-specific** - Development, staging, production configs

### Infrastructure Layer

- **HTTP Client** - Configured with Polly policies
- **Caching** - Memory cache for rules and responses
- **Logging** - Structured logging with Serilog
- **Health Checks** - System health monitoring

## 🔒 Security

### Authentication

- **Azure DevOps PAT** - Secure token-based authentication
- **Environment Variables** - Secure credential storage
- **No Hardcoded Secrets** - All sensitive data externalized

### Data Protection

- **No Data Persistence** - No sensitive data stored locally
- **Secure Communication** - HTTPS-only API communication
- **Input Validation** - Comprehensive input sanitization

## 🧪 Testing

### Unit Tests

```bash
dotnet test tests/CodeReviewRunner.Tests/
```

### Integration Tests

```bash
dotnet test tests/CodeReviewRunner.IntegrationTests/
```

### Load Testing

```bash
# Use tools like NBomber or k6 for load testing
dotnet run --project tests/LoadTests/
```

## 📈 Performance

### Benchmarks

- **File Analysis**: ~100ms per file
- **API Calls**: ~200ms per Azure DevOps request
- **Memory Usage**: ~50MB baseline
- **Concurrent Files**: Up to 20 files simultaneously

### Optimization

- **Parallel Processing** - Configurable concurrency
- **Caching** - Rules and API response caching
- **Connection Pooling** - HTTP client reuse
- **Memory Management** - Proper disposal patterns

## 🚀 Deployment

### Azure DevOps Pipelines

```yaml
trigger:
  - main

pool:
  vmImage: "ubuntu-latest"

variables:
  buildConfiguration: "Release"

steps:
  - task: DotNetCoreCLI@2
    displayName: "Restore packages"
    inputs:
      command: "restore"
      projects: "**/*.csproj"

  - task: DotNetCoreCLI@2
    displayName: "Build"
    inputs:
      command: "build"
      projects: "**/*.csproj"
      arguments: "--configuration $(buildConfiguration)"

  - task: DotNetCoreCLI@2
    displayName: "Test"
    inputs:
      command: "test"
      projects: "**/*.csproj"
      arguments: '--configuration $(buildConfiguration) --collect:"XPlat Code Coverage"'

  - task: DotNetCoreCLI@2
    displayName: "Publish"
    inputs:
      command: "publish"
      projects: "src/CodeReviewRunner/CodeReviewRunner.csproj"
      arguments: "--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)"
```

### Docker Deployment

```bash
# Build Docker image
docker build -t codereviewrunner:latest .

# Run container
docker run -e SYSTEM_ACCESSTOKEN=your-token codereviewrunner:latest
```

## 📚 API Reference

### CodeReviewService

```csharp
public interface ICodeReviewService
{
    Task<CodeReviewResult> AnalyzePullRequestAsync(
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        CancellationToken cancellationToken = default);

    Task<CodeReviewResult> AnalyzeLocalFilesAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default);
}
```

### AzureDevOpsService

```csharp
public interface IAzureDevOpsService
{
    Task<List<(string path, string content)>> GetPullRequestChangedFilesAsync(
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        CancellationToken cancellationToken = default);

    Task<bool> TestRepositoryAccessAsync(
        string organization,
        string project,
        string repositoryId,
        CancellationToken cancellationToken = default);
}
```

## 🔧 Troubleshooting

### Common Issues

1. **Authentication Errors**

   - Verify SYSTEM_ACCESSTOKEN is set correctly
   - Check PAT permissions in Azure DevOps
   - Ensure token hasn't expired

2. **API Rate Limiting**

   - Implement exponential backoff
   - Use caching to reduce API calls
   - Monitor rate limit headers

3. **Memory Issues**
   - Adjust MaxConcurrentFiles setting
   - Monitor memory usage in production
   - Consider file size limits

### Debug Mode

```bash
# Enable debug logging
export Logging__LogLevel__Default=Debug
dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj
```

### Health Checks

```bash
# Check application health
curl http://localhost:5000/health
```

## 📞 Support

### Documentation

- [Configuration Guide](docs/configuration.md)
- [API Reference](docs/api-reference.md)
- [Deployment Guide](docs/deployment.md)
- [Troubleshooting](docs/troubleshooting.md)

### Community

- [GitHub Issues](https://github.com/yourorg/codereviewrunner/issues)
- [Discussions](https://github.com/yourorg/codereviewrunner/discussions)
- [Wiki](https://github.com/yourorg/codereviewrunner/wiki)

### Enterprise Support

- **Email**: enterprise-support@yourorg.com
- **Phone**: +1-800-ENTERPRISE
- **Portal**: https://support.yourorg.com

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

See [CONTRIBUTING.md](CONTRIBUTING.md) for detailed guidelines.

---

**CodeReviewRunner Enterprise v2.0** - Built for scale, designed for enterprise.
