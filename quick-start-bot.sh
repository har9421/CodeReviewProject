#!/bin/bash

# Quick Start Script for Intelligent C# Code Review Bot
# This script helps you set up and run the bot quickly

echo "🤖 Intelligent C# Code Review Bot - Quick Start"
echo "=============================================="
echo ""

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found. Please install .NET 8 SDK first."
    echo "   Visit: https://dotnet.microsoft.com/download"
    exit 1
fi

# Check if ngrok is installed
if ! command -v ngrok &> /dev/null; then
    echo "❌ ngrok not found. Please install ngrok first."
    echo "   1. Sign up at: https://dashboard.ngrok.com/signup"
    echo "   2. Install: brew install ngrok/ngrok/ngrok (macOS) or download from website"
    echo "   3. Configure: ngrok config add-authtoken YOUR_TOKEN"
    exit 1
fi

# Check if Azure DevOps PAT is set
if [ -z "$AZURE_DEVOPS_PAT" ]; then
    echo "⚠️  AZURE_DEVOPS_PAT environment variable not set."
    echo "   Please set your Personal Access Token:"
    echo "   export AZURE_DEVOPS_PAT=\"your-token-here\""
    echo ""
    read -p "Do you want to continue without PAT? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

echo "✅ Prerequisites check passed!"
echo ""

# Build the project
echo "🔨 Building the bot..."
cd src/CodeReviewBot
dotnet build
if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please check the errors above."
    exit 1
fi

echo "✅ Build successful!"
echo ""

# Start the bot in background
echo "🚀 Starting the bot service..."
dotnet run --urls="http://localhost:5000" &
BOT_PID=$!

# Wait a moment for the bot to start
sleep 3

# Check if bot is running
if ! curl -s http://localhost:5000/api/webhook/health > /dev/null; then
    echo "❌ Bot failed to start. Check the logs above."
    kill $BOT_PID 2>/dev/null
    exit 1
fi

echo "✅ Bot is running on http://localhost:5000"
echo ""

# Start ngrok
echo "🌐 Starting ngrok tunnel..."
ngrok http 5000 &
NGROK_PID=$!

# Wait for ngrok to start
sleep 5

# Get ngrok URL
NGROK_URL=$(curl -s http://localhost:4040/api/tunnels | grep -o '"public_url":"https://[^"]*' | cut -d'"' -f4 | head -1)

if [ -z "$NGROK_URL" ]; then
    echo "❌ Failed to get ngrok URL. Please check ngrok configuration."
    kill $BOT_PID $NGROK_PID 2>/dev/null
    exit 1
fi

echo "✅ ngrok tunnel active: $NGROK_URL"
echo ""

# Display next steps
echo "🎉 Bot is ready! Next steps:"
echo "=============================="
echo ""
echo "1. Test the bot health:"
echo "   curl $NGROK_URL/api/webhook/health"
echo ""
echo "2. Configure Azure DevOps webhook:"
echo "   ./configure-webhook-simple.ps1 -OrganizationUrl \"https://dev.azure.com/YOUR_ORG\" -ProjectName \"YOUR_PROJECT\" -PersonalAccessToken \"YOUR_PAT\" -BotServiceUrl \"$NGROK_URL\""
echo ""
echo "3. Create a test pull request with C# code"
echo "4. Watch the bot analyze and comment!"
echo ""
echo "📋 Bot Information:"
echo "   • Local URL: http://localhost:5000"
echo "   • Public URL: $NGROK_URL"
echo "   • Health Check: $NGROK_URL/api/webhook/health"
echo "   • Logs: logs/codereviewbot-*.log"
echo ""
echo "🛑 To stop the bot:"
echo "   Press Ctrl+C or run: kill $BOT_PID $NGROK_PID"
echo ""

# Keep script running and show logs
echo "📊 Bot logs (Press Ctrl+C to stop):"
echo "===================================="
tail -f logs/codereviewbot-*.log 2>/dev/null || echo "No log files found yet. Create a PR to see activity."
