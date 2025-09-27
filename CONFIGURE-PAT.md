# Configure Personal Access Token (PAT)

## 🔧 **PAT Configuration via Environment Variables**

Your bot reads the Personal Access Token from the `AZURE_DEVOPS_PAT` environment variable for secure configuration.

## 📝 **How to Configure PAT**

### **1. Create a PAT in Azure DevOps**

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

### **2. Set Environment Variable**

#### **Option A: Using the startup script (Recommended)**

```bash
# Use the provided script that sets the PAT
./start-bot-with-pat.sh "your-pat-token-here"
```

#### **Option B: Manual environment variable setup**

```bash
# Set the PAT environment variable
export AZURE_DEVOPS_PAT="your-actual-pat-token-here"

# Start the bot
export NGROK_URL="https://ngrok.io"
cd src/CodeReviewBot.Presentation
dotnet run
```

#### **Option C: Create a startup script**

Create a file called `start-bot.sh`:

```bash
#!/bin/bash
export AZURE_DEVOPS_PAT="your-pat-token-here"
export NGROK_URL="https://ngrok.io"
cd src/CodeReviewBot.Presentation
dotnet run
```

Make it executable and run:

```bash
chmod +x start-bot.sh
./start-bot.sh
```

### **3. Verify Configuration**

Check if the PAT is set correctly:

```bash
echo $AZURE_DEVOPS_PAT
```

## 🎯 **Benefits of This Approach**

✅ **Security**: PAT is not stored in configuration files  
✅ **Environment Isolation**: Different PATs for dev/staging/prod  
✅ **No Version Control Risk**: PAT never accidentally committed  
✅ **Standard Practice**: Follows .NET configuration best practices

## 🔒 **Security Best Practices**

### **For Development:**

- Use environment variables or `.env` files
- Never commit PAT tokens to version control
- Use different PATs for different environments

### **For Production:**

- Use secure secret management (Azure Key Vault, AWS Secrets Manager)
- Set environment variables in your deployment pipeline
- Rotate PATs regularly

## 📋 **Example Environment Setup**

```bash
# Set your PAT
export AZURE_DEVOPS_PAT="your-pat-token-here"

# Set ngrok flag for proxy compatibility
export NGROK_URL="https://ngrok.io"

# Navigate to bot directory
cd src/CodeReviewBot.Presentation

# Start the bot
dotnet run
```

## 🛠️ **Available Startup Scripts**

The project includes several startup scripts for different scenarios:

- **`start-bot-with-pat.sh`**: Sets PAT and starts bot
- **`start-bot-ngrok.sh`**: Starts bot with ngrok compatibility
- **`test-webhook.sh`**: Tests webhook endpoints

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
