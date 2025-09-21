# 🚀 GitHub Codespaces Deployment Guide

This guide shows you how to deploy and use the Code Review Bot with Azure DevOps PRs using GitHub Codespaces.

## 🎯 **Why GitHub Codespaces?**

- ✅ **Free for Students**: Unlimited usage with GitHub Student Pack
- ✅ **Free Tier**: 120 hours/month for regular users
- ✅ **Pre-configured Environment**: .NET 8, ngrok, and tools ready
- ✅ **Cloud-based**: No local setup required
- ✅ **Integrated**: Works seamlessly with GitHub repositories

## 📋 **Prerequisites**

1. **GitHub Account** (free)
2. **GitHub Codespaces Access**:
   - Free: 120 hours/month
   - Student: Unlimited with GitHub Student Pack
   - Pro: 180 hours/month
3. **Azure DevOps Account** (free)
4. **ngrok Account** (free at ngrok.com)

## 🚀 **Step 1: Set Up GitHub Repository**

### **Option A: Push Your Code to GitHub**

1. **Initialize Git Repository**:

   ```bash
   git init
   git add .
   git commit -m "Initial commit: Code Review Bot"
   ```

2. **Create GitHub Repository**:

   - Go to [github.com](https://github.com)
   - Click "New repository"
   - Name it: `codereview-bot`
   - Make it public or private (your choice)

3. **Push to GitHub**:
   ```bash
   git remote add origin https://github.com/YOUR_USERNAME/codereview-bot.git
   git branch -M main
   git push -u origin main
   ```

### **Option B: Use Existing Repository**

If you already have the code in GitHub, you're ready to go!

## 🌐 **Step 2: Create GitHub Codespace**

1. **Navigate to Your Repository**:

   - Go to your GitHub repository
   - Click the green "Code" button
   - Select "Codespaces" tab
   - Click "Create codespace on main"

2. **Wait for Codespace Creation**:

   - Codespace will automatically:
     - Install .NET 8 SDK
     - Set up the development environment
     - Clone your repository

3. **Open in Browser or VS Code**:
   - Browser: Automatic
   - VS Code: Install "GitHub Codespaces" extension

## ⚙️ **Step 3: Configure Codespace Environment**

### **Install ngrok**

```bash
# In the Codespace terminal
curl -s https://ngrok-agent.s3.amazonaws.com/ngrok.asc | sudo tee /etc/apt/trusted.gpg.d/ngrok.asc >/dev/null
echo "deb https://ngrok-agent.s3.amazonaws.com buster main" | sudo tee /etc/apt/sources.list.d/ngrok.list
sudo apt update && sudo apt install ngrok

# Authenticate ngrok (you'll need your auth token from ngrok.com)
ngrok authtoken YOUR_NGROK_AUTH_TOKEN
```

### **Set Up Environment Variables**

```bash
# Create environment file
cat > .env << EOF
Bot__Webhook__Secret=your-secret-key-here
Bot__DefaultRulesUrl=coding-standards.json
Bot__Name=Intelligent C# Code Review Bot
ASPNETCORE_ENVIRONMENT=Development
EOF

# Load environment variables
export $(cat .env | xargs)
```

## 🔧 **Step 4: Build and Run the Bot**

### **Build the Project**

```bash
cd src/CodeReviewBot
dotnet build -c Release
```

### **Run the Bot Service**

```bash
# Start the bot service
dotnet run --urls="http://0.0.0.0:5000" &

# Wait for service to start
sleep 5

# Test the service
curl http://localhost:5000/api/webhook/health
```

### **Start ngrok Tunnel**

```bash
# In a new terminal or background process
ngrok http 5000 --log=stdout &
```

### **Get Public URL**

```bash
# Get the ngrok public URL
curl -s http://localhost:4040/api/tunnels | grep -o '"public_url":"[^"]*' | grep -o 'https://[^"]*'
```

## 🔗 **Step 5: Configure Azure DevOps Webhook**

### **Get Your Bot URL**

Your bot will be accessible at: `https://abc123.ngrok.io`

### **Configure Webhook**

```bash
# Use the configure-webhook script
./configure-webhook.ps1 -OrganizationUrl "https://dev.azure.com/yourorg" -ProjectName "YourProject" -PersonalAccessToken "your-pat" -BotServiceUrl "https://abc123.ngrok.io"
```

### **Manual Configuration**

1. Go to Azure DevOps → Project Settings → Service hooks
2. Create subscription → Web Hooks
3. Select "Pull request created" and "Pull request updated"
4. Set URL to: `https://your-ngrok-url.ngrok.io/api/webhook`
5. Test the connection

## 📊 **Step 6: Test the Integration**

### **Test Health Endpoint**

```bash
curl https://your-ngrok-url.ngrok.io/api/webhook/health
```

Expected response:

```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "message": "Code Review Bot is running"
}
```

### **Test with Pull Request**

1. Create a new pull request in your Azure DevOps project
2. Check the Codespace terminal for webhook events
3. The bot should log the PR information

## 🔄 **Step 7: Create Automation Script**

Create a startup script to automate the process:

```bash
# Create startup script
cat > start-bot.sh << 'EOF'
#!/bin/bash

echo "🚀 Starting Code Review Bot in Codespace..."

# Load environment variables
export $(cat .env | xargs)

# Build the project
cd src/CodeReviewBot
echo "📦 Building project..."
dotnet build -c Release

# Start the bot service
echo "🌐 Starting bot service..."
dotnet run --urls="http://0.0.0.0:5000" &
BOT_PID=$!

# Wait for service to start
sleep 5

# Test the service
if curl -s http://localhost:5000/api/webhook/health > /dev/null; then
    echo "✅ Bot service started successfully"
else
    echo "❌ Bot service failed to start"
    exit 1
fi

# Start ngrok
echo "🔗 Starting ngrok tunnel..."
ngrok http 5000 --log=stdout &
NGROK_PID=$!

# Wait for ngrok to start
sleep 3

# Get the public URL
NGROK_URL=$(curl -s http://localhost:4040/api/tunnels | grep -o '"public_url":"[^"]*' | grep -o 'https://[^"]*')

if [ -z "$NGROK_URL" ]; then
    echo "❌ Failed to get ngrok URL"
    exit 1
fi

echo ""
echo "🎉 Code Review Bot is running!"
echo "=================================="
echo "Local URL: http://localhost:5000"
echo "Public URL: $NGROK_URL"
echo "Webhook Endpoint: $NGROK_URL/api/webhook"
echo "Health Check: $NGROK_URL/api/webhook/health"
echo ""
echo "📋 Next Steps:"
echo "1. Configure webhook in Azure DevOps with URL: $NGROK_URL"
echo "2. Create a test pull request"
echo "3. Check logs for webhook events"
echo ""
echo "Press Ctrl+C to stop"

# Keep running
wait
EOF

# Make it executable
chmod +x start-bot.sh
```

## 🎯 **Step 8: Run Everything**

```bash
# Run the automated startup script
./start-bot.sh
```

## 📱 **Step 9: Access from Anywhere**

Since Codespaces runs in the cloud:

- **Access from Any Device**: Use any computer with internet
- **Share with Team**: Share the Codespace URL
- **Always Available**: No need to keep your computer running
- **Automatic Backups**: Your code is safely stored in GitHub

## 🔧 **Advanced Configuration**

### **Persistent Storage**

Codespaces automatically persists:

- Your code changes
- Environment variables (if saved to files)
- Configuration files

### **Custom Dev Container**

Create `.devcontainer/devcontainer.json` for custom setup:

```json
{
  "name": "Code Review Bot",
  "image": "mcr.microsoft.com/devcontainers/dotnet:8.0",
  "features": {
    "ghcr.io/devcontainers/features/git:1": {},
    "ghcr.io/devcontainers/features/github-cli:1": {}
  },
  "postCreateCommand": "dotnet restore",
  "forwardPorts": [5000, 4040],
  "portsAttributes": {
    "5000": {
      "label": "Bot Service",
      "onAutoForward": "notify"
    },
    "4040": {
      "label": "ngrok Dashboard",
      "onAutoForward": "silent"
    }
  }
}
```

### **Environment Variables**

Save to `.env` file for persistence:

```bash
# .env file
Bot__Webhook__Secret=your-secret-key-here
Bot__DefaultRulesUrl=coding-standards.json
Bot__Name=Intelligent C# Code Review Bot
NGROK_AUTH_TOKEN=your-ngrok-token
```

## 📊 **Monitoring and Logs**

### **View Logs**

```bash
# Bot service logs
tail -f logs/codereviewbot-*.log

# ngrok logs
curl http://localhost:4040/api/requests/http
```

### **Health Monitoring**

```bash
# Check bot health
curl https://your-ngrok-url.ngrok.io/api/webhook/health

# Check ngrok status
curl http://localhost:4040/api/tunnels
```

## 💡 **Tips for Codespaces**

### **Cost Optimization**

- Stop Codespaces when not in use
- Use free tier efficiently (120 hours/month)
- Consider GitHub Student Pack for unlimited usage

### **Performance**

- Codespaces automatically scales resources
- Use port forwarding for external access
- Leverage GitHub's global infrastructure

### **Collaboration**

- Share Codespace URLs with team members
- Use GitHub Issues for bug tracking
- Collaborate in real-time

## 🔄 **Daily Workflow**

1. **Start Codespace**: Open your repository → Codespaces → Resume
2. **Run Bot**: Execute `./start-bot.sh`
3. **Get URL**: Copy the ngrok URL
4. **Configure Webhook**: Use the URL in Azure DevOps
5. **Test**: Create PRs and monitor logs
6. **Stop**: Ctrl+C to stop, Codespace auto-saves

## 🆘 **Troubleshooting**

### **Common Issues**

| Issue                     | Solution                                        |
| ------------------------- | ----------------------------------------------- |
| **Codespace won't start** | Check GitHub status, restart Codespace          |
| **ngrok not working**     | Verify auth token, check ngrok status           |
| **Bot not accessible**    | Check port forwarding, verify ngrok URL         |
| **Webhook failures**      | Test health endpoint, check Azure DevOps config |

### **Support Commands**

```bash
# Check Codespace status
gh codespace list

# View Codespace logs
gh codespace logs

# Restart Codespace
gh codespace stop && gh codespace create
```

---

**🎉 You now have a fully functional Code Review Bot running in GitHub Codespaces!**

This setup gives you a professional development environment without any local setup, and you can access it from anywhere with an internet connection.
