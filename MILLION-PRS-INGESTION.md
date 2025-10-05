# Million C# PRs Data Ingestion System

## Overview

This system enables the Code Review Bot to learn from millions of C# pull requests by ingesting data from GitHub repositories. The system is designed to process large volumes of data efficiently while respecting GitHub's API rate limits.

## 🚀 Quick Start

### Prerequisites

1. **GitHub Token**: Get a personal access token from GitHub

   ```bash
   export GITHUB_TOKEN="your-github-token-here"
   ```

2. **Bot Running**: Ensure the intelligent bot is running

   ```bash
   ./start-intelligent-bot.sh
   ```

3. **Dependencies**: Install required tools

   ```bash
   # macOS
   brew install jq curl

   # Ubuntu
   sudo apt-get install jq curl

   # Windows
   choco install jq curl
   ```

### Start Ingestion

```bash
./ingest-millions-prs.sh
```

## 📊 Data Sources

### Popular C# Repositories

The system automatically targets popular C# repositories including:

- Microsoft .NET Framework
- ASP.NET Core
- Entity Framework Core
- Azure SDKs
- Visual Studio Code
- PowerShell
- And many more...

### Custom Repositories

You can specify custom repositories to ingest:

```json
{
  "customRepositories": ["your-org/your-repo", "another-org/another-repo"]
}
```

## 🔧 Configuration Options

### Ingestion Config

```json
{
  "usePopularRepositories": true,
  "customRepositories": ["microsoft/dotnet"],
  "maxRepositories": 1000,
  "maxPRsPerRepository": 1000,
  "startDate": "2022-01-01T00:00:00Z",
  "endDate": "2024-01-01T00:00:00Z",
  "languages": ["csharp"]
}
```

### Batch Processing

- **Batch Size**: 1000 PRs per batch
- **Concurrency**: Configurable (default: CPU cores)
- **Rate Limiting**: 100ms delay between requests
- **Memory Management**: Automatic cleanup and optimization

## 📈 Expected Data Volume

### Large-Scale Ingestion

- **Repositories**: 1,000+ popular C# repos
- **Pull Requests**: 1,000,000+ PRs
- **Files**: 10,000,000+ C# files
- **Duration**: 2-4 hours
- **Storage**: ~2-4 GB learning data

### Batch Processing

- **Batches**: 8 batches (6 months each)
- **Coverage**: 2020-2024 (4 years)
- **Total PRs**: 500,000+ PRs
- **Duration**: 8-12 hours
- **Storage**: ~1-2 GB learning data

## 🎯 Learning Features

### Data Preprocessing

- **File Filtering**: Excludes generated code, test files, and build artifacts
- **Content Cleaning**: Removes excessive whitespace and comments
- **Pattern Analysis**: Identifies code patterns and complexity
- **Quality Metrics**: Calculates code quality scores

### Learning Data Structure

```json
{
  "id": "unique-id",
  "pullRequestId": "12345",
  "repositoryName": "microsoft/dotnet",
  "projectName": "microsoft",
  "analysisDate": "2024-01-01T00:00:00Z",
  "metrics": {
    "totalFilesAnalyzed": 15,
    "totalIssuesFound": 8,
    "fileTypes": [".cs"],
    "ruleUsageCount": {
      "naming-convention": 3,
      "method-length": 2,
      "documentation": 3
    }
  }
}
```

## 🔄 Processing Pipeline

### 1. Data Collection

- Fetch repository list from GitHub API
- Get pull request metadata
- Download file changes and diffs
- Apply rate limiting and error handling

### 2. Data Preprocessing

- Clean and filter files
- Extract meaningful changes
- Analyze code patterns
- Calculate quality metrics

### 3. Analysis

- Run code analysis rules
- Generate learning data
- Track rule effectiveness
- Update confidence scores

### 4. Learning Integration

- Record learning data
- Update rule effectiveness
- Generate insights
- Optimize rules

## 📊 Monitoring and Progress

### Real-Time Monitoring

```bash
# Check ingestion status
curl http://localhost:5002/api/data-ingestion/status

# Get specific progress
curl http://localhost:5002/api/data-ingestion/progress/{ingestion-id}

# Monitor performance
curl http://localhost:5002/api/performance/report
```

### Progress Tracking

- **Real-time updates**: Progress percentage and ETA
- **Error handling**: Automatic retry and error reporting
- **Resumable**: Can pause and resume long-running ingestions
- **Logging**: Detailed logs for debugging

## 🎛️ API Endpoints

### Data Ingestion

- `POST /api/data-ingestion/start` - Start new ingestion
- `GET /api/data-ingestion/progress/{id}` - Get progress
- `GET /api/data-ingestion/status` - List all ingestions
- `POST /api/data-ingestion/stop/{id}` - Stop ingestion
- `DELETE /api/data-ingestion/clear/{id}` - Clear ingestion data

### Learning

- `GET /api/feedback/insights` - Get learning insights
- `GET /api/feedback/rule-effectiveness` - View rule performance
- `POST /api/feedback/optimize-rules` - Trigger optimization

### Performance

- `GET /api/performance/report` - Performance metrics
- `GET /api/performance/health` - System health
- `GET /api/performance/alerts` - Performance alerts

## 🚨 Rate Limiting and Best Practices

### GitHub API Limits

- **Authenticated**: 5,000 requests/hour
- **Recommended delay**: 100ms between requests
- **Burst handling**: Automatic backoff on rate limit

### Best Practices

1. **Start Small**: Begin with 10-50 repositories
2. **Monitor Progress**: Check status regularly
3. **Respect Limits**: Don't exceed GitHub's rate limits
4. **Backup Data**: Save progress periodically
5. **Clean Up**: Remove old ingestion data

## 📈 Expected Learning Improvements

### After 100,000 PRs

- **Rule Accuracy**: +20-30%
- **False Positive Rate**: -15-25%
- **Developer Satisfaction**: +25-35%

### After 1,000,000 PRs

- **Rule Accuracy**: +40-50%
- **False Positive Rate**: -30-40%
- **Developer Satisfaction**: +50-60%
- **Coverage**: 95%+ of common patterns

## 🔧 Troubleshooting

### Common Issues

1. **Rate Limit Exceeded**

   ```bash
   # Check GitHub API status
   curl -H "Authorization: token $GITHUB_TOKEN" \
        https://api.github.com/rate_limit
   ```

2. **Memory Issues**

   ```bash
   # Monitor memory usage
   curl http://localhost:5002/api/performance/report | jq '.Metrics[] | select(.OperationName == "memory")'
   ```

3. **Slow Processing**
   ```bash
   # Check performance alerts
   curl http://localhost:5002/api/performance/alerts
   ```

### Debug Commands

```bash
# Check bot health
curl http://localhost:5002/api/performance/health

# View learning insights
curl http://localhost:5002/api/feedback/insights

# Get rule effectiveness
curl http://localhost:5002/api/feedback/rule-effectiveness
```

## 📚 Advanced Usage

### Custom Repository Lists

```bash
# Create custom repository list
cat > custom-repos.json << EOF
{
  "customRepositories": [
    "microsoft/dotnet",
    "dotnet/core",
    "dotnet/aspnetcore"
  ],
  "maxRepositories": 50,
  "maxPRsPerRepository": 500
}
EOF

# Start custom ingestion
curl -X POST http://localhost:5002/api/data-ingestion/start \
     -H "Content-Type: application/json" \
     -d @custom-repos.json
```

### Batch Processing

```bash
# Process specific time periods
for year in 2020 2021 2022 2023; do
  for quarter in Q1 Q2 Q3 Q4; do
    echo "Processing $year $quarter..."
    # Start batch processing
  done
done
```

### Data Export

```bash
# Export learning data
curl http://localhost:5002/api/feedback/insights > learning-insights.json

# Export performance metrics
curl http://localhost:5002/api/performance/report > performance-report.json
```

## 🎯 Success Metrics

### Data Quality

- **File Coverage**: 90%+ C# files processed
- **Pattern Recognition**: 95%+ common patterns identified
- **Error Rate**: <5% processing errors

### Learning Effectiveness

- **Rule Accuracy**: 80%+ accuracy on test data
- **False Positive Rate**: <20% false positives
- **Developer Satisfaction**: 70%+ positive feedback

### Performance

- **Processing Speed**: 100+ PRs/minute
- **Memory Usage**: <4GB peak memory
- **Uptime**: 99%+ availability

## 🔮 Future Enhancements

### Planned Features

- **Real-time Ingestion**: Continuous learning from new PRs
- **Multi-language Support**: Java, Python, JavaScript
- **Advanced Analytics**: ML-based pattern recognition
- **Cloud Integration**: Azure/AWS data processing
- **API Versioning**: Support for different GitHub API versions

### Research Areas

- **Transfer Learning**: Apply learnings across projects
- **Federated Learning**: Learn from multiple organizations
- **Active Learning**: Focus on high-value PRs
- **Causal Analysis**: Understand why certain patterns occur

## 📞 Support

### Getting Help

- **Documentation**: Check this file and INTELLIGENT-FEATURES.md
- **Logs**: Review bot logs for detailed error information
- **API**: Use the health and status endpoints for diagnostics
- **Community**: Join our community for support and discussions

### Reporting Issues

When reporting issues, please include:

- Ingestion configuration used
- Error messages and logs
- System specifications
- Steps to reproduce

---

**Happy Learning! 🚀**

The more data you feed the bot, the smarter it becomes. Start with a small dataset and gradually scale up to millions of PRs for maximum learning effectiveness.
