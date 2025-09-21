# 🆓 Free Deployment Guide for Code Review Bot

This guide shows you how to use the Code Review Bot with Azure DevOps PRs without needing an Azure subscription.

## 🎯 **Quick Start Options**

### **Option 1: Local Development with ngrok (Recommended)**

This is the fastest way to get started - completely free!

#### **Prerequisites:**

- .NET 8 SDK
- ngrok account (free)

#### **Step 1: Install ngrok**

```bash
# Download from https://ngrok.com/download
# Or use package manager:
brew install ngrok        # macOS
choco install ngrok       # Windows
```

#### **Step 2: Run the Bot**

```bash
# Option A: Use the automated script
./start-local-bot.sh      # macOS/Linux
.\start-local-bot.ps1     # Windows PowerShell

# Option B: Manual steps
cd src/CodeReviewBot
dotnet run --urls="http://localhost:5000"

# In another terminal:
ngrok http 5000
```

#### **Step 3: Configure Azure DevOps Webhook**

```powershell
# Use the ngrok URL from step 2
.\configure-webhook.ps1 -OrganizationUrl "https://dev.azure.com/yourorg" -ProjectName "YourProject" -PersonalAccessToken "your-pat" -BotServiceUrl "https://abc123.ngrok.io"
```

### **Option 2: Free Cloud Platforms**

#### **A. Railway (Free Tier)**

1. Sign up at [railway.app](https://railway.app)
2. Connect your GitHub repository
3. Deploy with one click
4. Get your public URL

#### **B. Render (Free Tier)**

1. Sign up at [render.com](https://render.com)
2. Create a new Web Service
3. Connect your repository
4. Deploy automatically

#### **C. Fly.io (Free Tier)**

1. Sign up at [fly.io](https://fly.io)
2. Install flyctl CLI
3. Deploy with: `fly launch`

#### **D. Heroku Alternatives**

- Railway
- Render
- Fly.io
- Vercel (for static hosting)

## 🚀 **Step-by-Step: Using with Azure DevOps PRs**

### **Step 1: Deploy Your Bot**

Choose one of the free options above and get your public URL.

### **Step 2: Configure Azure DevOps Webhook**

#### **Method A: Using PowerShell Script**

```powershell
.\configure-webhook.ps1 -OrganizationUrl "https://dev.azure.com/yourorg" -ProjectName "YourProject" -PersonalAccessToken "your-pat" -BotServiceUrl "https://your-bot-url.com"
```

#### **Method B: Manual Configuration**

1. Go to Azure DevOps → Project Settings → Service hooks
2. Create subscription → Web Hooks
3. Select "Pull request created" and "Pull request updated"
4. Set URL to: `https://your-bot-url.com/api/webhook`
5. Test the connection

### **Step 3: Test the Integration**

1. **Test Health Endpoint:**

   ```bash
   curl https://your-bot-url.com/api/webhook/health
   ```

2. **Create a Test PR:**

   - Create a new pull request in your Azure DevOps project
   - The bot should automatically receive the webhook
   - Check your bot logs for webhook events

3. **Verify Webhook Events:**
   - Check Azure DevOps Service Hooks page
   - Look for "Last triggered" timestamp
   - Review bot service logs

## 🔧 **Configuration**

### **Environment Variables**

Set these in your deployment platform:

| Variable               | Description              | Example                          |
| ---------------------- | ------------------------ | -------------------------------- |
| `Bot__Webhook__Secret` | Webhook signature secret | `your-secret-key`                |
| `Bot__DefaultRulesUrl` | Path to coding standards | `coding-standards.json`          |
| `Bot__Name`            | Bot display name         | `Intelligent C# Code Review Bot` |

### **Coding Standards**

The bot uses the included `coding-standards.json` file with 27 predefined rules:

- **Naming Conventions**: Methods, classes, interfaces, properties
- **Code Complexity**: Size limits, parameter counts, nesting depth
- **Performance**: String concatenation, memory management
- **Security**: SQL injection prevention, hardcoded secrets
- **Async Patterns**: Proper async/await usage
- **Error Handling**: Exception handling best practices
- **Code Style**: Formatting, documentation, consistency

## 📊 **Monitoring and Troubleshooting**

### **Health Checks**

```bash
# Test if bot is running
curl https://your-bot-url.com/api/webhook/health

# Expected response:
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "message": "Code Review Bot is running"
}
```

### **Common Issues**

| Issue                            | Solution                                                |
| -------------------------------- | ------------------------------------------------------- |
| **Webhook not receiving events** | Check webhook URL is accessible, verify PAT permissions |
| **Signature validation failed**  | Ensure webhook secret matches in bot configuration      |
| **Bot not responding**           | Test health endpoint, check application logs            |
| **ngrok URL changes**            | ngrok free tier generates new URLs on restart           |

### **Logs**

**Local Development:**

```bash
# Check bot logs in terminal where you started it
# Or check logs directory
tail -f logs/codereviewbot-*.log
```

**Cloud Platforms:**

- Railway: Check deployment logs in dashboard
- Render: View logs in service dashboard
- Fly.io: Use `fly logs`

## 💡 **Tips for Free Usage**

### **ngrok Limitations**

- Free tier has session limits (8 hours)
- URLs change when restarting
- Consider paid tier for production use

### **Cloud Platform Limits**

- Railway: 500 hours/month free
- Render: 750 hours/month free
- Fly.io: Limited resources on free tier

### **Best Practices**

1. **Use for Development/Testing**: Perfect for learning and testing
2. **Monitor Usage**: Keep track of your free tier limits
3. **Backup Configuration**: Save your webhook settings
4. **Test Regularly**: Verify bot functionality with test PRs

## 🔄 **Next Steps**

Once you're comfortable with the free setup:

1. **Customize Rules**: Edit `coding-standards.json` for your team
2. **Add AI Analysis**: Configure OpenAI API for intelligent suggestions
3. **Scale Up**: Consider paid tiers for production use
4. **Team Rollout**: Share with your development team
5. **Extend Functionality**: Add more analysis rules or integrations

## 🆘 **Getting Help**

### **Common Commands**

```bash
# Test bot health
curl https://your-bot-url.com/api/webhook/health

# Check ngrok status (if using ngrok)
curl http://localhost:4040/api/tunnels

# View bot logs locally
tail -f logs/codereviewbot-*.log
```

### **Support Resources**

- Check bot service logs for errors
- Verify webhook configuration in Azure DevOps
- Test endpoints manually with curl
- Review deployment platform logs

---

**🎉 You can now use the Code Review Bot with Azure DevOps PRs completely free!**

Start with the local ngrok setup for the quickest experience, then consider free cloud platforms for more stability.
