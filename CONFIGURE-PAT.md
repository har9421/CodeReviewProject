# Configure Personal Access Token (PAT)

## 🔧 **Easy PAT Configuration**

Your bot now reads the Personal Access Token from the `appsettings.json` configuration file instead of requiring environment variables.

## 📝 **How to Configure PAT**

### **1. Edit appsettings.json**

Open `/src/CodeReviewBot.Presentation/appsettings.json` and find the `Bot.AzureDevOps.PersonalAccessToken` field:

```json
{
  "Bot": {
    "AzureDevOps": {
      "PersonalAccessToken": "YOUR_PAT_TOKEN_HERE"
    }
  }
}
```

### **2. Create a PAT in Azure DevOps**

1. Go to: https://dev.azure.com/khUniverse/_usersSettings/tokens
2. Click **"New Token"**
3. Configure the token:
   - **Name**: "Code Review Bot"
   - **Expiration**: Choose appropriate duration
   - **Scopes**: 
     - ✅ **Code (read & write)**
     - ✅ **Pull Requests (read & write)**
4. Click **"Create"**
5. **Copy the token** (you won't see it again!)

### **3. Update appsettings.json**

Replace `"YOUR_PAT_TOKEN_HERE"` with your actual PAT:

```json
{
  "Bot": {
    "AzureDevOps": {
      "PersonalAccessToken": "your-actual-pat-token-here"
    }
  }
}
```

### **4. Restart the Bot**

```bash
# Stop current bot
pkill -f "CodeReviewBot.Presentation"

# Start with new configuration
export NGROK_URL="https://ngrok.io"
cd src/CodeReviewBot.Presentation
dotnet run
```

## 🎯 **Benefits of This Approach**

✅ **Easy Configuration**: No need to set environment variables  
✅ **Version Control Safe**: Keep PAT in appsettings.Development.json  
✅ **Multiple Environments**: Different configs for dev/staging/prod  
✅ **Centralized Settings**: All bot configuration in one place  

## 🔒 **Security Best Practices**

### **For Development:**
- Use `appsettings.Development.json` for local development
- Add `appsettings.Development.json` to `.gitignore`

### **For Production:**
- Use environment variables or Azure Key Vault
- Never commit PAT tokens to version control

## 📋 **Example Configuration**

```json
{
  "Bot": {
    "Name": "Intelligent C# Code Review Bot",
    "Version": "1.0.0",
    "AzureDevOps": {
      "BaseUrl": "https://dev.azure.com",
      "ApiVersion": "7.0",
      "PersonalAccessToken": "your-pat-token",
      "TimeoutSeconds": 30,
      "RetryAttempts": 3
    },
    "Analysis": {
      "MaxConcurrentFiles": 10,
      "SupportedFileExtensions": [".cs"],
      "MaxFileSizeKB": 1024
    },
    "Notifications": {
      "EnableComments": true,
      "MaxCommentsPerFile": 50
    }
  }
}
```

## ✅ **Test Your Configuration**

After configuring the PAT, test with a webhook:

```bash
curl -X POST "https://your-ngrok-url.ngrok-free.app/api/webhook/health"
```

You should see the bot respond successfully, and when you create a pull request, the bot will:
- ✅ Fetch the pull request details
- ✅ Analyze the changed C# files
- ✅ Post intelligent comments with findings

## 🚀 **You're Ready!**

Your bot is now configured to perform full code analysis on Azure DevOps pull requests!
