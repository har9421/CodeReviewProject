# 🚀 Direct Webhook Deployment Guide

This guide shows you how to deploy the Code Review Bot using the direct webhook approach (no Publisher ID required).

## 📋 Prerequisites

- Azure DevOps organization with admin access
- Personal Access Token (PAT) with the following permissions:
  - `Code (read & write)`
  - `Build (read & execute)`
  - `Service Connections (read)`
  - `Project and team (read)`
  - `Service hooks (read & manage)`

## 🎯 Deployment Options

### Option 1: Azure App Service (Recommended)

1. **Create Azure App Service**:

   ```bash
   # Using Azure CLI
   az webapp create --resource-group myResourceGroup --plan myAppServicePlan --name mycodereviewbot --runtime "DOTNET|8.0"
   ```

2. **Deploy the Bot**:

   ```bash
   # Zip the dist folder
   cd dist
   zip -r ../codereview-bot.zip .
   cd ..

   # Deploy to Azure App Service
   az webapp deployment source config-zip --resource-group myResourceGroup --name mycodereviewbot --src codereview-bot.zip
   ```

3. **Configure Environment Variables**:
   - Go to Azure Portal → App Service → Configuration
   - Add these application settings:
     ```
     Bot__Webhook__Secret = "your-secret-key-here"
     Bot__DefaultRulesUrl = "coding-standards.json"
     ```

### Option 2: Docker Container

1. **Create Dockerfile**:

   ```dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:8.0
   WORKDIR /app
   COPY dist/ .
   EXPOSE 80
   ENTRYPOINT ["dotnet", "CodeReviewBot.dll"]
   ```

2. **Build and Run**:
   ```bash
   docker build -t codereview-bot .
   docker run -p 8080:80 -e Bot__Webhook__Secret="your-secret" codereview-bot
   ```

### Option 3: Local Development

1. **Run Locally**:

   ```bash
   cd dist
   dotnet CodeReviewBot.dll
   ```

2. **Use ngrok for Testing**:
   ```bash
   ngrok http 5000
   # Use the https URL for webhook configuration
   ```

## 🔗 Configure Webhooks

### Step 1: Get Your Bot Service URL

- **Azure App Service**: `https://your-app-name.azurewebsites.net`
- **Docker/Local**: `https://your-domain.com` or ngrok URL
- **Webhook Endpoint**: `{bot-service-url}/api/webhook`

### Step 2: Configure Webhook in Azure DevOps

Use the provided PowerShell script:

```powershell
.\configure-webhook.ps1 -OrganizationUrl "https://dev.azure.com/yourorg" -ProjectName "YourProject" -PersonalAccessToken "your-pat" -BotServiceUrl "https://your-bot-service.azurewebsites.net"
```

### Step 3: Manual Webhook Configuration

If you prefer to configure manually:

1. Go to Azure DevOps → Project Settings → Service hooks
2. Click "Create subscription"
3. Select "Web Hooks" as the service
4. Configure the webhook:
   - **URL**: `https://your-bot-service.azurewebsites.net/api/webhook`
   - **Events**: Select "Pull request created" and "Pull request updated"
   - **Filters**: Select your repository

## 🧪 Testing the Bot

### Step 1: Test Health Endpoint

```bash
curl https://your-bot-service.azurewebsites.net/api/webhook/health
```

Expected response:

```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "message": "Code Review Bot is running"
}
```

### Step 2: Test with a Pull Request

1. Create a new pull request in your Azure DevOps repository
2. Check the bot service logs for webhook events
3. The bot should log the PR information (full analysis implementation pending)

## 📊 Monitoring

### Application Logs

- **Azure App Service**: Go to Monitoring → Log stream
- **Docker**: Use `docker logs <container-id>`
- **Local**: Check console output

### Health Checks

- **Health endpoint**: `GET /api/webhook/health`
- **Swagger UI** (Development): `GET /swagger` (if enabled)

## 🔧 Configuration

### Environment Variables

| Variable               | Description              | Default                          |
| ---------------------- | ------------------------ | -------------------------------- |
| `Bot__Webhook__Secret` | Webhook signature secret | Required                         |
| `Bot__DefaultRulesUrl` | Path to coding standards | `coding-standards.json`          |
| `Bot__Name`            | Bot display name         | `Intelligent C# Code Review Bot` |

### Coding Standards

The bot uses the `coding-standards.json` file included in the project. You can:

1. **Use default file**: No changes needed
2. **Customize rules**: Edit the file to match your team's standards
3. **Use remote file**: Set `Bot__DefaultRulesUrl` to a URL

## 🚨 Troubleshooting

### Common Issues

1. **Webhook not receiving events**:

   - Check webhook URL is correct and accessible
   - Verify Personal Access Token permissions
   - Check bot service logs for errors

2. **Signature validation failed**:

   - Ensure `Bot__Webhook__Secret` is set correctly
   - Verify the secret matches in Azure DevOps webhook configuration

3. **Bot not responding**:
   - Check health endpoint: `/api/webhook/health`
   - Verify all dependencies are deployed
   - Check application logs for startup errors

### Debug Mode

Enable detailed logging by setting:

```
Serilog__MinimumLevel__Default = "Debug"
```

## 🔄 Next Steps

Once your bot is working with webhooks:

1. **Implement Code Analysis**: Add the actual code analysis logic
2. **Add AI Integration**: Configure OpenAI API for intelligent suggestions
3. **Create Extension**: Use `install-bot.ps1` to create a full Azure DevOps extension
4. **Team Rollout**: Share with your team for wider adoption

## 📞 Support

- Check logs first: Most issues are visible in application logs
- Verify configuration: Ensure all required settings are configured
- Test endpoints: Use health check to verify service is running

---

**🎉 Your Code Review Bot is now ready to receive webhooks from Azure DevOps!**
