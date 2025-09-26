# 🤖 Code Review Bot - Complete Setup Guide

Your **Intelligent C# Code Review Bot** is now fully implemented with actual code analysis capabilities! This guide will help you set it up and start analyzing pull requests.

## 🎉 What's New

The bot now includes:

- ✅ **Full Code Analysis**: Analyzes C# files using your coding standards
- ✅ **Azure DevOps Integration**: Fetches PR changes and posts comments
- ✅ **Smart Commenting**: Posts contextual feedback with suggestions
- ✅ **Coding Standards**: Uses the included `coding-standards.json` file
- ✅ **Summary Reports**: Provides analysis summaries for each PR

## 🚀 Quick Setup

### Step 1: Set Up Azure DevOps Personal Access Token

1. **Go to Azure DevOps**: Navigate to your organization
2. **Create PAT**: Go to User Settings → Personal Access Tokens
3. **Create New Token** with these permissions:

   - **Code (Read & Write)**
   - **Pull Requests (Read & Write)**
   - **Project and Team (Read)**

4. **Set Environment Variable**:
   ```bash
   export AZURE_DEVOPS_PAT="your-personal-access-token-here"
   ```

### Step 2: Set Up ngrok (if not already done)

1. **Sign up for ngrok**: https://dashboard.ngrok.com/signup (free)
2. **Get your authtoken**: https://dashboard.ngrok.com/get-started/your-authtoken
3. **Configure ngrok**:
   ```bash
   ngrok config add-authtoken YOUR_AUTHTOKEN_HERE
   ```

### Step 3: Start the Bot

1. **Start the bot service**:

   ```bash
   cd src/CodeReviewBot
   dotnet run --urls="http://localhost:5000"
   ```

2. **In a new terminal, start ngrok**:

   ```bash
   ngrok http 5000
   ```

3. **Copy your ngrok URL** (e.g., `https://abc123.ngrok-free.app`)

### Step 4: Configure Azure DevOps Webhook

Use the simplified webhook configuration script:

```powershell
.\configure-webhook-simple.ps1 -OrganizationUrl "https://dev.azure.com/YOUR_ORG" -ProjectName "YOUR_PROJECT" -PersonalAccessToken "YOUR_PAT" -BotServiceUrl "https://YOUR_NGROK_URL.ngrok-free.app"
```

**Example**:

```powershell
.\configure-webhook-simple.ps1 -OrganizationUrl "https://dev.azure.com/mycompany" -ProjectName "MyProject" -PersonalAccessToken "abc123..." -BotServiceUrl "https://abc123.ngrok-free.app"
```

## 🧪 Testing the Bot

### Test 1: Health Check

```bash
curl https://YOUR_NGROK_URL.ngrok-free.app/api/webhook/health
```

**Expected Response**:

```json
{
  "status": "healthy",
  "timestamp": "2024-01-20T10:30:00Z",
  "message": "Code Review Bot is running"
}
```

### Test 2: Create a Test Pull Request

1. **Create a branch** with some C# code that violates coding standards
2. **Create a pull request** in Azure DevOps
3. **Watch the bot analyze** and post comments!

### Test 3: Monitor Logs

Check your bot logs for analysis activity:

```bash
tail -f logs/codereviewbot-*.log
```

## 📋 What the Bot Analyzes

The bot uses the included `coding-standards.json` file with 27+ rules covering:

### 🎯 **Naming Conventions**

- Method names (PascalCase)
- Class names (PascalCase)
- Property names (PascalCase)
- Field names (camelCase)
- Variable names (camelCase)
- Constant names (PascalCase)

### 🔍 **Code Quality**

- Method complexity (too long, too many parameters)
- Class complexity (too many methods)
- Nested if depth
- String concatenation patterns
- Missing XML documentation

### 🔒 **Security**

- Hardcoded connection strings
- SQL injection patterns
- Hardcoded secrets detection

### ⚡ **Performance**

- String concatenation in loops
- Memory management patterns
- Async/await best practices

### 🛠 **Error Handling**

- Exception handling patterns
- Try-catch best practices
- Resource disposal patterns

## 🎨 Bot Comment Examples

### Method Naming Issue

```
🤖 **Intelligent C# Code Review Bot**

**Warning**: Method names should be PascalCase

💡 **Suggestion**: Rename 'calculateTotal' to 'CalculateTotal'

📋 **Rule**: method-naming
```

### String Concatenation Issue

```
🤖 **Intelligent C# Code Review Bot**

**Info**: String concatenation detected. Consider using StringBuilder or string interpolation.

💡 **Suggestion**: Use StringBuilder for multiple concatenations or $"...\" for interpolation

📋 **Rule**: string-concatenation
```

### Analysis Summary

```
🤖 **Intelligent C# Code Review Bot Analysis Summary**

Found **5** code quality issues:

• **Errors**: 0
• **Warnings**: 3
• **Info**: 2

Files analyzed: 3
Comments posted: 5
```

## ⚙️ Configuration Options

### Bot Settings (`appsettings.json`)

```json
{
  "Bot": {
    "Name": "Intelligent C# Code Review Bot",
    "DefaultRulesUrl": "coding-standards.json",
    "Analysis": {
      "MaxConcurrentFiles": 10,
      "CacheRulesMinutes": 60,
      "EnableCaching": true,
      "SupportedFileExtensions": [".cs"],
      "MaxFileSizeKB": 1024
    },
    "Notifications": {
      "EnableComments": true,
      "EnableSummary": true,
      "MaxCommentsPerFile": 50
    }
  }
}
```

### Custom Coding Standards

1. **Edit the included file**: `coding-standards.json`
2. **Or use a remote URL**: Update `DefaultRulesUrl` in `appsettings.json`
3. **Or use environment variable**: `Bot__DefaultRulesUrl`

## 🔧 Troubleshooting

### Issue: "AZURE_DEVOPS_PAT environment variable not set"

**Solution**: Set your Personal Access Token:

```bash
export AZURE_DEVOPS_PAT="your-token-here"
```

### Issue: "No C# files changed in PR"

**Solution**: The bot only analyzes `.cs` files. Make sure your PR includes C# code changes.

### Issue: "Failed to fetch PR details"

**Solution**: Check your PAT permissions and organization URL format.

### Issue: Bot not posting comments

**Solution**:

1. Verify webhook is configured correctly
2. Check bot logs for errors
3. Ensure PAT has write permissions

### Issue: ngrok URL changes

**Solution**: Free ngrok URLs change on restart. For production, consider:

- Paid ngrok subscription
- Deploy to cloud (Azure, AWS, etc.)
- Use GitHub Codespaces

## 📊 Monitoring & Logs

### Log Files

- **Location**: `logs/codereviewbot-YYYY-MM-DD.log`
- **Rotation**: Daily (keeps 30 days)
- **Level**: Information and above

### Key Log Messages

```
[INFO] Processing PR 123 in MyProject/MyRepo
[INFO] Found 5 issues across 3 files in PR 123
[INFO] Posted comment for issue method-naming in file Program.cs:15
[INFO] PR 123 analysis completed successfully. Found 5 issues, posted 5 comments.
```

## 🚀 Next Steps

### Immediate Actions

1. ✅ Set up your Azure DevOps PAT
2. ✅ Start the bot with ngrok
3. ✅ Configure webhooks
4. ✅ Test with a sample PR

### Future Enhancements

- **AI Integration**: Enable AI-powered analysis in `appsettings.json`
- **Custom Rules**: Add your team's specific coding standards
- **Learning**: Enable learning from team feedback
- **Metrics**: Track analysis statistics and trends
- **Cloud Deployment**: Deploy to Azure App Service or AWS

## 🆘 Need Help?

1. **Check the logs**: `logs/codereviewbot-*.log`
2. **Test health endpoint**: `curl https://your-ngrok-url/api/webhook/health`
3. **Verify webhook configuration** in Azure DevOps Service Hooks
4. **Review this guide** for common issues and solutions

---

**🎉 Congratulations!** Your Intelligent C# Code Review Bot is now fully operational and ready to analyze pull requests!
