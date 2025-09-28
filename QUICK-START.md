# 🚀 Code Review Bot - Quick Start Guide

## ⚡ One-Command Setup

Run this single command to start everything:

```bash
./complete-setup.sh
```

This script will:

- ✅ Kill all existing processes
- ✅ Set up PAT environment variables
- ✅ Start the bot on port 5002
- ✅ Start ngrok tunnel
- ✅ Provide Azure DevOps webhook configuration instructions

## 🛑 Stop Everything

To stop all processes:

```bash
./stop-all.sh
```

## 📋 What the Complete Setup Script Does

### Step 1: Clean Slate

- Kills all existing CodeReviewBot processes
- Kills all dotnet run processes
- Kills all ngrok processes
- Frees up ports 5000 and 5002

### Step 2: Environment Setup

- Sets `AZURE_DEVOPS_PAT` with your token
- Sets `NGROK_URL` for proxy compatibility

### Step 3: Start Bot

- Navigates to bot directory
- Starts bot on port 5002 (avoids macOS AirPlay conflict)
- Tests bot health endpoint
- Runs in background

### Step 4: Start ngrok

- Starts ngrok tunnel on port 5002
- Gets public HTTPS URL
- Tests ngrok endpoint
- Provides webhook configuration instructions

## 🔧 Manual Commands (if needed)

### Start Bot Only

```bash
export AZURE_DEVOPS_PAT="your-pat-token-here"
export NGROK_URL="https://ngrok.io"
cd src/CodeReviewBot.Presentation
dotnet run --urls="http://localhost:5002"
```

### Start ngrok Only

```bash
ngrok http 5002
```

### Test Bot Health

```bash
curl -s http://localhost:5002/api/webhook/health
```

### Test ngrok Endpoint

```bash
curl -s https://your-ngrok-url.ngrok-free.app/api/webhook/health
```

## 🎯 Azure DevOps Webhook Configuration

After running the setup script, you'll get instructions like:

1. **Go to**: https://dev.azure.com/khUniverse
2. **Navigate to**: Project Settings → Service hooks
3. **Create subscriptions for**:
   - Git pull request created
   - Git pull request updated
4. **Use the provided ngrok URL** as the webhook endpoint

## 📊 Monitoring

### Check Bot Status

```bash
ps aux | grep "CodeReviewBot\|dotnet\|ngrok" | grep -v grep
```

### View Logs

```bash
tail -f src/CodeReviewBot.Presentation/logs/codereviewbot-*.log
```

### Check ngrok Status

```bash
curl -s http://localhost:4040/api/tunnels | python3 -m json.tool
```

## 🚨 Troubleshooting

### Port Conflicts

- **Port 5000**: Used by macOS AirPlay - avoid this port
- **Port 5002**: Recommended for the bot
- Use `lsof -ti:PORT | xargs kill -9` to free ports

### 403 Errors

- Usually means port conflict with AirPlay
- Restart with port 5002: `./complete-setup.sh`

### 401 Errors

- PAT not configured or expired
- Check PAT with: `./test-pat.sh`

### ngrok Issues

- ngrok URLs change when restarted
- Update Azure DevOps webhooks after restarting ngrok

## 🎉 Success Indicators

You'll know everything is working when:

- ✅ Bot responds to health checks
- ✅ ngrok tunnel is active
- ✅ Webhook receives test events
- ✅ Bot logs show "Received webhook" messages

## 📝 Notes

- Keep the terminal open to maintain processes
- ngrok URLs change when restarted
- Update webhooks if you restart ngrok
- Bot analyzes C# files and posts intelligent comments

**Your Code Review Bot is ready to analyze pull requests!** 🚀
