# 🔗 Azure DevOps Integration Guide

This guide shows you how to integrate your deployed Code Review Bot with Azure DevOps using webhooks.

## 📋 Prerequisites

- ✅ Code Review Bot deployed to a cloud service (Azure App Service, AWS, Docker, etc.)
- ✅ Azure DevOps organization with admin access
- ✅ Personal Access Token (PAT) with the following permissions:
  - `Code (read & write)`
  - `Build (read & execute)`
  - `Service Connections (read)`
  - `Project and team (read)`
  - `Service hooks (read & manage)`

## 🚀 Step-by-Step Integration

### **Step 1: Get Your Bot Service URL**

After deploying your bot, you'll have a public URL like:

- **Azure App Service**: `https://your-bot-name.azurewebsites.net`
- **AWS/Azure VM**: `https://your-domain.com`
- **Docker/Container**: `https://your-container-url.com`

Your webhook endpoint will be: `{your-bot-url}/api/webhook`

### **Step 2: Test Your Bot Service**

Before configuring webhooks, test that your bot is running:

```bash
# Test health endpoint
curl https://your-bot-service.azurewebsites.net/api/webhook/health

# Expected response:
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "message": "Code Review Bot is running"
}
```

### **Step 3: Configure Webhook in Azure DevOps**

#### **Method A: Using PowerShell Script (Recommended)**

1. **Run the webhook configuration script**:

   ```powershell
   .\configure-webhook.ps1 -OrganizationUrl "https://dev.azure.com/yourorg" -ProjectName "YourProject" -PersonalAccessToken "your-pat" -BotServiceUrl "https://your-bot-service.azurewebsites.net"
   ```

2. **The script will automatically**:
   - Create webhook subscriptions for pull request events
   - Configure proper headers and authentication
   - Set up both "created" and "updated" pull request events

#### **Method B: Manual Configuration**

1. **Navigate to Azure DevOps**:

   - Go to your Azure DevOps organization
   - Select your project
   - Go to **Project Settings** → **Service hooks**

2. **Create New Subscription**:

   - Click **"Create subscription"**
   - Select **"Web Hooks"** as the service
   - Click **"Next"**

3. **Configure Events**:

   - Select **"Pull request created"** and **"Pull request updated"**
   - Click **"Next"**

4. **Configure Filters** (Optional):

   - Select specific repositories if needed
   - Set branch filters if required
   - Click **"Next"**

5. **Configure Webhook**:
   - **URL**: `https://your-bot-service.azurewebsites.net/api/webhook`
   - **HTTP Headers** (Optional):
     ```
     Content-Type: application/json
     X-Webhook-Secret: your-secret-key
     ```
   - Click **"Test"** to verify the connection
   - Click **"Finish"**

### **Step 4: Verify Integration**

1. **Check Webhook Status**:

   - Go to **Project Settings** → **Service hooks**
   - Verify your webhook subscription is active
   - Check the "Last triggered" timestamp

2. **Test with a Pull Request**:
   - Create a new pull request in your repository
   - Check your bot service logs for webhook events
   - The bot should log the PR information

### **Step 5: Monitor and Troubleshoot**

#### **Check Bot Service Logs**

**Azure App Service**:

```bash
# View logs in Azure Portal
# Or use Azure CLI:
az webapp log tail --name your-bot-name --resource-group your-resource-group
```

**Docker Container**:

```bash
docker logs your-container-name
```

#### **Common Issues and Solutions**

| Issue                                  | Solution                                                                                                                               |
| -------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| **Webhook not receiving events**       | - Check webhook URL is correct and accessible<br>- Verify PAT has "Service hooks" permissions<br>- Check bot service logs for errors   |
| **Signature validation failed**        | - Ensure webhook secret matches in bot configuration<br>- Check if bot service is configured with correct secret                       |
| **Bot not responding**                 | - Test health endpoint: `/api/webhook/health`<br>- Check application logs for startup errors<br>- Verify all dependencies are deployed |
| **Pull request events not triggering** | - Verify webhook is configured for correct events<br>- Check if repository/branch filters are too restrictive                          |

## 🔧 Configuration Options

### **Environment Variables**

Configure these in your bot service:

| Variable               | Description              | Example                          |
| ---------------------- | ------------------------ | -------------------------------- |
| `Bot__Webhook__Secret` | Webhook signature secret | `your-secret-key-here`           |
| `Bot__DefaultRulesUrl` | Path to coding standards | `coding-standards.json`          |
| `Bot__Name`            | Bot display name         | `Intelligent C# Code Review Bot` |

### **Webhook Events**

The bot currently handles these Azure DevOps events:

- `git.pullrequest.created` - When a new pull request is created
- `git.pullrequest.updated` - When a pull request is updated

### **Coding Standards**

The bot uses the included `coding-standards.json` file with 27 predefined rules covering:

- Naming conventions
- Code complexity
- Performance patterns
- Security practices
- Async/await patterns
- Error handling
- Code style

## 📊 Monitoring

### **Health Checks**

- **Endpoint**: `GET /api/webhook/health`
- **Response**: JSON with status and timestamp
- **Use Case**: Monitor bot availability

### **Logs**

- **Location**: `logs/codereviewbot-{date}.log`
- **Format**: Structured JSON logs with timestamps
- **Levels**: Information, Warning, Error

### **Metrics** (Future Enhancement)

The bot can be extended to provide:

- Number of PRs analyzed
- Issues found per category
- Response times
- Error rates

## 🔄 Next Steps

Once your bot is integrated and working:

1. **Customize Rules**: Edit `coding-standards.json` to match your team's standards
2. **Add AI Analysis**: Configure OpenAI API for intelligent suggestions
3. **Team Training**: Share the bot with your development team
4. **Monitor Usage**: Track how the bot helps improve code quality
5. **Extend Functionality**: Add more analysis rules or integrate with other tools

## 🆘 Support

### **Getting Help**

1. **Check Logs**: Most issues are visible in application logs
2. **Test Endpoints**: Use health check to verify service status
3. **Verify Configuration**: Ensure all required settings are configured
4. **Review Webhooks**: Check Azure DevOps webhook configuration

### **Useful Commands**

```bash
# Test bot health
curl https://your-bot-service.azurewebsites.net/api/webhook/health

# Check webhook configuration in Azure DevOps
# Go to Project Settings → Service hooks

# View bot logs (Azure App Service)
az webapp log tail --name your-bot-name --resource-group your-resource-group

# View bot logs (Docker)
docker logs your-container-name
```

---

**🎉 Your Code Review Bot is now integrated with Azure DevOps and ready to analyze pull requests!**
