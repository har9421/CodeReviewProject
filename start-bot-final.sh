#!/bin/bash

# Stop any existing bot processes
pkill -f "CodeReviewBot.Presentation"

# Set environment variables
export AZURE_DEVOPS_PAT="your-pat-token-here"
export NGROK_URL="https://ngrok.io"

echo "🚀 Starting bot with PAT configured..."
echo "📁 Working directory: $(pwd)"
echo "🔑 PAT configured: ${AZURE_DEVOPS_PAT:0:10}..."
echo "🌐 NGROK_URL: $NGROK_URL"
echo ""

# Navigate to the presentation project and start the bot
cd src/CodeReviewBot.Presentation

# Start the bot with environment variables
dotnet run

echo ""
echo "✅ Bot process ended!"
