#!/bin/bash

# Complete Code Review Bot Setup Script
# This script kills all processes, starts bot with PAT, starts ngrok, and configures webhooks

echo "🚀 Starting Complete Code Review Bot Setup..."
echo "================================================"

# Configuration
PAT_TOKEN="your-pat-token-here"
BOT_PORT=5002

echo ""
echo "📋 Step 1: Killing all existing processes..."
echo "--------------------------------------------"

# Kill all existing processes
echo "🛑 Stopping CodeReviewBot processes..."
pkill -f "CodeReviewBot.Presentation" 2>/dev/null || echo "   No CodeReviewBot processes found"

echo "🛑 Stopping dotnet run processes..."
pkill -f "dotnet run" 2>/dev/null || echo "   No dotnet run processes found"

echo "🛑 Stopping ngrok processes..."
pkill -f "ngrok" 2>/dev/null || echo "   No ngrok processes found"

echo "🛑 Stopping any processes on port $BOT_PORT..."
lsof -ti:$BOT_PORT | xargs kill -9 2>/dev/null || echo "   No processes found on port $BOT_PORT"

echo "✅ All processes stopped successfully!"
echo ""

# Wait a moment for processes to fully stop
sleep 2

echo "📋 Step 2: Setting up environment variables..."
echo "---------------------------------------------"

# Set environment variables
export AZURE_DEVOPS_PAT="$PAT_TOKEN"
export NGROK_URL="https://ngrok.io"

echo "🔑 PAT configured: ${PAT_TOKEN:0:10}..."
echo "🌐 NGROK_URL: $NGROK_URL"
echo "✅ Environment variables set!"
echo ""

echo "📋 Step 3: Starting the Code Review Bot..."
echo "------------------------------------------"

# Navigate to the bot directory
cd src/CodeReviewBot.Presentation

echo "📁 Working directory: $(pwd)"
echo "🚀 Starting bot on port $BOT_PORT..."

# Start the bot in background
dotnet run --urls="http://localhost:$BOT_PORT" &
BOT_PID=$!

echo "✅ Bot started with PID: $BOT_PID"
echo ""

# Wait for bot to start
echo "⏳ Waiting for bot to initialize..."
sleep 5

# Test if bot is running
echo "🧪 Testing bot health..."
HEALTH_RESPONSE=$(curl -s http://localhost:$BOT_PORT/api/webhook/health 2>/dev/null)

if [[ $HEALTH_RESPONSE == *"healthy"* ]]; then
    echo "✅ Bot is healthy and running!"
else
    echo "❌ Bot health check failed. Response: $HEALTH_RESPONSE"
    echo "🛑 Stopping setup due to bot failure..."
    kill $BOT_PID 2>/dev/null
    exit 1
fi

echo ""

echo "📋 Step 4: Starting ngrok tunnel..."
echo "-----------------------------------"

# Navigate back to project root
cd ../..

# Start ngrok in background
echo "🌐 Starting ngrok tunnel on port $BOT_PORT..."
ngrok http $BOT_PORT &
NGROK_PID=$!

echo "✅ ngrok started with PID: $NGROK_PID"
echo ""

# Wait for ngrok to initialize
echo "⏳ Waiting for ngrok to initialize..."
sleep 5

# Get ngrok URL
echo "🔍 Getting ngrok public URL..."
NGROK_URL=$(curl -s http://localhost:4040/api/tunnels | python3 -c "
import sys, json
try:
    data = json.load(sys.stdin)
    if data['tunnels']:
        for tunnel in data['tunnels']:
            if tunnel['proto'] == 'https':
                print(tunnel['public_url'])
                break
    else:
        print('NO_TUNNELS')
except:
    print('ERROR')
" 2>/dev/null)

if [[ "$NGROK_URL" == "NO_TUNNELS" || "$NGROK_URL" == "ERROR" || -z "$NGROK_URL" ]]; then
    echo "❌ Failed to get ngrok URL. Please check ngrok status manually."
    echo "🛑 Stopping setup due to ngrok failure..."
    kill $BOT_PID 2>/dev/null
    kill $NGROK_PID 2>/dev/null
    exit 1
fi

echo "✅ ngrok tunnel active: $NGROK_URL"
echo ""

# Test ngrok endpoint
echo "🧪 Testing ngrok endpoint..."
WEBHOOK_URL="$NGROK_URL/api/webhook"
HEALTH_URL="$NGROK_URL/api/webhook/health"

HEALTH_RESPONSE=$(curl -s "$HEALTH_URL" 2>/dev/null)

if [[ $HEALTH_RESPONSE == *"healthy"* ]]; then
    echo "✅ ngrok endpoint is working!"
else
    echo "❌ ngrok endpoint test failed. Response: $HEALTH_RESPONSE"
    echo "⚠️  Bot may still be working, but ngrok tunnel has issues."
fi

echo ""

echo "🎉 SETUP COMPLETE!"
echo "=================="
echo ""
echo "📊 Current Status:"
echo "  🤖 Bot: Running on port $BOT_PORT (PID: $BOT_PID)"
echo "  🌐 ngrok: Active (PID: $NGROK_PID)"
echo "  🔗 Public URL: $NGROK_URL"
echo "  🔑 PAT: Configured and ready"
echo ""
echo "🧪 Test URLs:"
echo "  Health Check: $HEALTH_URL"
echo "  Webhook Endpoint: $WEBHOOK_URL"
echo ""

echo "📋 Next Steps - Configure Azure DevOps Webhooks:"
echo "================================================"
echo ""
echo "1. Go to your Azure DevOps project:"
echo "   https://dev.azure.com/khUniverse"
echo ""
echo "2. Navigate to Project Settings → Service hooks"
echo ""
echo "3. Create new subscription with these settings:"
echo "   📡 Event: Git pull request created"
echo "   🔗 URL: $WEBHOOK_URL"
echo "   📝 HTTP Headers: Content-Type: application/json"
echo ""
echo "4. Create another subscription for:"
echo "   📡 Event: Git pull request updated"
echo "   🔗 URL: $WEBHOOK_URL"
echo "   📝 HTTP Headers: Content-Type: application/json"
echo ""
echo "5. Test by creating a pull request in your repository!"
echo ""

echo "🛠️  Management Commands:"
echo "========================"
echo ""
echo "Stop all processes:"
echo "  pkill -f 'CodeReviewBot.Presentation'"
echo "  pkill -f 'ngrok'"
echo ""
echo "Check bot status:"
echo "  curl -s http://localhost:$BOT_PORT/api/webhook/health"
echo ""
echo "Check ngrok status:"
echo "  curl -s http://localhost:4040/api/tunnels"
echo ""
echo "View bot logs:"
echo "  tail -f src/CodeReviewBot.Presentation/logs/codereviewbot-*.log"
echo ""

echo "⚠️  Important Notes:"
echo "===================="
echo "• Keep this terminal open to maintain the bot and ngrok processes"
echo "• ngrok URLs change when you restart ngrok"
echo "• Update Azure DevOps webhooks if you restart ngrok"
echo "• The bot will analyze C# files in pull requests and post comments"
echo ""

echo "🎯 Your Code Review Bot is ready to analyze pull requests!"
echo "   Create a test PR to see it in action! 🚀"
