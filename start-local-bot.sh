#!/bin/bash

# Code Review Bot - Local Development Setup
# This script helps you run the bot locally with ngrok for Azure DevOps integration

echo "🚀 Starting Code Review Bot locally..."

# Check if ngrok is installed
if ! command -v ngrok &> /dev/null; then
    echo "❌ ngrok is not installed. Please install it first:"
    echo "   - Download from: https://ngrok.com/download"
    echo "   - Or install via package manager:"
    echo "     macOS: brew install ngrok"
    echo "     Windows: choco install ngrok"
    echo "     Linux: https://ngrok.com/download"
    exit 1
fi

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET is not installed. Please install .NET 8 SDK first:"
    echo "   - Download from: https://dotnet.microsoft.com/download"
    exit 1
fi

echo "✅ Prerequisites check passed"

# Build the project
echo "📦 Building the bot service..."
cd src/CodeReviewBot
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please check the errors above."
    exit 1
fi

echo "✅ Build successful"

# Start the bot service in background
echo "🌐 Starting bot service on http://localhost:5000..."
dotnet run --urls="http://localhost:5000" &
BOT_PID=$!

# Wait for the service to start
echo "⏳ Waiting for service to start..."
sleep 5

# Test if the service is running
if curl -s http://localhost:5000/api/webhook/health > /dev/null; then
    echo "✅ Bot service is running successfully"
else
    echo "❌ Bot service failed to start"
    kill $BOT_PID 2>/dev/null
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
    kill $BOT_PID $NGROK_PID 2>/dev/null
    exit 1
fi

echo ""
echo "🎉 Code Review Bot is now running!"
echo "=================================="
echo "Local URL: http://localhost:5000"
echo "Public URL: $NGROK_URL"
echo "Webhook Endpoint: $NGROK_URL/api/webhook"
echo "Health Check: $NGROK_URL/api/webhook/health"
echo ""
echo "📋 Next Steps:"
echo "1. Configure webhook in Azure DevOps:"
echo "   .\configure-webhook.ps1 -OrganizationUrl \"https://dev.azure.com/yourorg\" -ProjectName \"YourProject\" -PersonalAccessToken \"your-pat\" -BotServiceUrl \"$NGROK_URL\""
echo ""
echo "2. Test the webhook:"
echo "   curl $NGROK_URL/api/webhook/health"
echo ""
echo "3. Create a pull request in your Azure DevOps project to test"
echo ""
echo "Press Ctrl+C to stop the bot and ngrok"

# Keep the script running
wait
