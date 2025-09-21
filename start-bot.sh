#!/bin/bash

echo "🚀 Starting Code Review Bot in Codespace..."

# Load environment variables if .env exists
if [ -f .env ]; then
    echo "📋 Loading environment variables..."
    export $(cat .env | xargs)
fi

# Set default values if not set
export Bot__Webhook__Secret=${Bot__Webhook__Secret:-"default-secret-key"}
export Bot__DefaultRulesUrl=${Bot__DefaultRulesUrl:-"coding-standards.json"}
export Bot__Name=${Bot__Name:-"Intelligent C# Code Review Bot"}

# Build the project
cd src/CodeReviewBot
echo "📦 Building project..."
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please check the errors above."
    exit 1
fi

# Start the bot service
echo "🌐 Starting bot service on http://0.0.0.0:5000..."
dotnet run --urls="http://0.0.0.0:5000" &
BOT_PID=$!

# Wait for service to start
echo "⏳ Waiting for service to start..."
sleep 5

# Test the service
if curl -s http://localhost:5000/api/webhook/health > /dev/null; then
    echo "✅ Bot service started successfully"
else
    echo "❌ Bot service failed to start"
    kill $BOT_PID 2>/dev/null
    exit 1
fi

# Check if ngrok is installed
if ! command -v ngrok &> /dev/null; then
    echo "📦 Installing ngrok..."
    curl -s https://ngrok-agent.s3.amazonaws.com/ngrok.asc | sudo tee /etc/apt/trusted.gpg.d/ngrok.asc >/dev/null
    echo "deb https://ngrok-agent.s3.amazonaws.com buster main" | sudo tee /etc/apt/sources.list.d/ngrok.list
    sudo apt update && sudo apt install -y ngrok
fi

# Authenticate ngrok if token is provided
if [ ! -z "$NGROK_AUTH_TOKEN" ]; then
    echo "🔐 Authenticating ngrok..."
    ngrok authtoken $NGROK_AUTH_TOKEN
fi

# Start ngrok
echo "🔗 Starting ngrok tunnel..."
ngrok http 5000 --log=stdout &
NGROK_PID=$!

# Wait for ngrok to start
sleep 3

# Get the public URL
echo "🌍 Getting public URL..."
NGROK_URL=$(curl -s http://localhost:4040/api/tunnels | grep -o '"public_url":"[^"]*' | grep -o 'https://[^"]*' | head -1)

if [ -z "$NGROK_URL" ]; then
    echo "❌ Failed to get ngrok URL"
    echo "💡 Make sure ngrok is authenticated with: ngrok authtoken YOUR_TOKEN"
    echo "💡 Or set NGROK_AUTH_TOKEN environment variable"
    kill $BOT_PID $NGROK_PID 2>/dev/null
    exit 1
fi

echo ""
echo "🎉 Code Review Bot is running!"
echo "=================================="
echo "Local URL: http://localhost:5000"
echo "Public URL: $NGROK_URL"
echo "Webhook Endpoint: $NGROK_URL/api/webhook"
echo "Health Check: $NGROK_URL/api/webhook/health"
echo "ngrok Dashboard: http://localhost:4040"
echo ""
echo "📋 Next Steps:"
echo "1. Configure webhook in Azure DevOps:"
echo "   URL: $NGROK_URL/api/webhook"
echo ""
echo "2. Test the webhook:"
echo "   curl $NGROK_URL/api/webhook/health"
echo ""
echo "3. Create a pull request in your Azure DevOps project"
echo ""
echo "4. Check logs for webhook events"
echo ""
echo "Press Ctrl+C to stop the bot and ngrok"

# Function to cleanup on exit
cleanup() {
    echo ""
    echo "🛑 Stopping services..."
    kill $BOT_PID $NGROK_PID 2>/dev/null
    echo "✅ Services stopped"
    exit 0
}

# Set trap for cleanup
trap cleanup SIGINT SIGTERM

# Keep running
wait
