# Intelligent C# Code Review Bot for Azure DevOps

An intelligent, AI-powered code review bot that installs directly into your Azure DevOps organization as an extension. The bot automatically reviews C# .NET Core code in pull requests and provides intelligent, contextual feedback.

## 🚀 Quick Start

### 1. Install the Bot Extension

Run the installation script:

```powershell
.\install-bot.ps1 -PublisherId "your-publisher-id" -OrganizationUrl "https://dev.azure.com/yourorg"
```

### 2. Configure Webhooks

After installation, configure webhooks for your project:

```powershell
.\configure-webhook.ps1 -OrganizationUrl "https://dev.azure.com/yourorg" -ProjectName "YourProject" -PersonalAccessToken "your-pat"
```

### 3. Set Up Coding Standards

Create a JSON file with your coding standards and host it at a publicly accessible URL:

```json
{
  "rules": [
    {
      "id": "method-naming",
      "severity": "warning",
      "message": "Method names should be in PascalCase",
      "languages": ["csharp"],
      "applies_to": "method_declaration"
    },
    {
      "id": "async-naming",
      "severity": "warning",
      "message": "Async methods should end with 'Async' suffix",
      "languages": ["csharp"],
      "applies_to": "method_declaration"
    }
  ]
}
```

### 4. Configure the Bot

Navigate to your Azure DevOps organization settings and configure the bot:

- **Coding Standards URL**: URL to your JSON rules file
- **AI Analysis**: Enable/disable AI-powered analysis
- **Learning & Adaptation**: Enable/disable learning from team feedback
- **Comment Settings**: Configure comment frequency and severity thresholds

## 🤖 Bot Features

### AI-Powered Analysis

- **Intelligent Code Review**: Uses AI to provide contextual, intelligent feedback
- **Smart Suggestions**: Generates specific, actionable suggestions with code examples
- **Architectural Insights**: Identifies design patterns and architectural concerns
- **Contextual Explanations**: Provides detailed explanations of why issues matter

### Learning & Adaptation

- **Team Learning**: Adapts to your team's coding patterns and preferences
- **Feedback Integration**: Learns from developer feedback to improve suggestions
- **Rule Adaptation**: Automatically adjusts rule severity based on team acceptance
- **Personalized Suggestions**: Provides team-specific recommendations

### Advanced C# Analysis

- **Roslyn Integration**: Uses Microsoft's Roslyn compiler for deep code analysis
- **Custom Rules**: Supports complex rules for naming, complexity, performance, security
- **Contextual Analysis**: Understands code context, method scope, and dependencies
- **Real-time Analysis**: Analyzes code as soon as pull requests are created/updated

### Intelligent Commenting

- **Contextual Comments**: Provides detailed, helpful comments with explanations
- **Learning Resources**: Includes links to documentation and best practices
- **Related Issues**: Identifies similar issues across the codebase
- **Summary Reports**: Posts comprehensive analysis summaries

## 📋 Installation Requirements

### Prerequisites

- Azure DevOps organization with admin access
- .NET 8 SDK
- Node.js and npm (for TFX CLI)
- PowerShell (for installation scripts)

### Bot Service Hosting

The bot requires a hosted service to receive webhooks and process code analysis. You can host it on:

- **Azure App Service** (recommended)
- **Azure Container Instances**
- **Docker containers**
- **Any cloud platform with HTTPS support**

## ⚙️ Configuration

### Environment Variables

Set these environment variables on your bot service:

```bash
# Required
SYSTEM_ACCESSTOKEN=your-azure-devops-pat

# Optional - AI Configuration
AI_API_KEY=your-openai-api-key
AI_MODEL=gpt-4
AI_ENABLED=true

# Optional - Learning Configuration
LEARNING_ENABLED=true
MIN_FEEDBACK_FOR_ADAPTATION=5

# Optional - Analysis Configuration
MAX_COMMENTS_PER_FILE=50
ENABLE_SUMMARY=true
SEVERITY_THRESHOLD=warning
```

### Bot Configuration API

Configure the bot programmatically:

```bash
curl -X POST "https://your-bot-service.azurewebsites.net/api/webhook/configure" \
  -H "Content-Type: application/json" \
  -d '{
    "organization": "yourorg",
    "project": "YourProject",
    "rulesUrl": "https://your-domain.com/coding-standards.json",
    "aiEnabled": true,
    "aiApiKey": "your-api-key",
    "learningEnabled": true,
    "maxCommentsPerFile": 50,
    "enableSummary": true,
    "severityThreshold": "warning"
  }'
```

## 🔧 Customization

### Custom Rules

Create sophisticated rules for your team:

```json
{
  "id": "complexity-analysis",
  "severity": "warning",
  "message": "Method complexity is too high",
  "languages": ["csharp"],
  "applies_to": "method_declaration",
  "ai_enhanced": true,
  "context_aware": true
}
```

### Custom AI Prompts

Modify the AI system prompt in your bot configuration:

```json
{
  "aiEnabled": true,
  "aiModel": "gpt-4",
  "systemPrompt": "You are an expert C# code reviewer focused on enterprise applications. Pay special attention to performance, security, and maintainability..."
}
```

### Learning Configuration

Customize learning behavior:

```json
{
  "learningEnabled": true,
  "minFeedbackForAdaptation": 10,
  "feedbackHistoryDays": 60,
  "lowHelpfulnessThreshold": 0.2,
  "highHelpfulnessThreshold": 0.8
}
```

## 📊 Monitoring & Analytics

### Health Checks

Monitor bot health:

```bash
curl https://your-bot-service.azurewebsites.net/api/webhook/health
```

### Metrics

The bot provides comprehensive metrics:

- **Analysis Performance**: Processing time and throughput
- **Issue Detection**: Types and frequency of issues found
- **Team Feedback**: Learning effectiveness and adaptation
- **Quality Trends**: Code quality improvements over time

### Logs

View detailed logs:

```bash
# Application logs
https://your-bot-service.azurewebsites.net/logs

# Analysis results
https://your-bot-service.azurewebsites.net/api/metrics
```

## 🔒 Security

### Webhook Security

- Webhooks are validated using HMAC-SHA256 signatures
- All communication is encrypted via HTTPS
- Personal Access Tokens are stored securely and rotated regularly

### Data Privacy

- Code analysis is performed in-memory
- No source code is permanently stored
- Analysis results are only posted as PR comments
- All data processing complies with GDPR and enterprise security requirements

## 🚀 Deployment Options

### Azure App Service

1. Create an Azure App Service
2. Deploy the bot service code
3. Configure environment variables
4. Set up webhook endpoints

### Docker Container

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY dist/ .
ENTRYPOINT ["dotnet", "CodeReviewBot.dll"]
```

### Azure Container Instances

```yaml
apiVersion: 2018-10-01
location: eastus
name: codereview-bot
properties:
  containers:
    - name: codereview-bot
      properties:
        image: your-registry/codereview-bot:latest
        resources:
          requests:
            cpu: 1
            memoryInGb: 2
  osType: Linux
  restartPolicy: Always
```

## 📞 Support

### Troubleshooting

Common issues and solutions:

**Bot not responding to webhooks:**

- Check webhook URL configuration
- Verify Personal Access Token permissions
- Review bot service logs

**AI analysis not working:**

- Verify API key is correct and has credits
- Check AI service configuration
- Review AI-specific logs

**Comments not appearing:**

- Verify PR comment permissions
- Check comment rate limits
- Review analysis results

### Getting Help

- **Documentation**: Check this README and inline help
- **Issues**: Create an issue in the repository
- **Support**: Contact support@your-domain.com

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Transform your C# code reviews with intelligent, AI-powered analysis that learns and adapts to your team's needs!**
