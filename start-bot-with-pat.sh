#!/bin/bash

# Check if PAT is provided
if [ -z "$1" ]; then
    echo "Usage: $0 <pat-token>"
    echo "Example: $0 your-pat-token-here"
    exit 1
fi

PAT_TOKEN="$1"

# Stop any existing bot processes
pkill -f "CodeReviewBot.Presentation"

# Set environment variables
export AZURE_DEVOPS_PAT="$PAT_TOKEN"
export NGROK_URL="https://ngrok.io"

# Navigate to the presentation project
cd src/CodeReviewBot.Presentation

echo "🚀 Starting bot with PAT configured..."
echo "📁 Working directory: $(pwd)"
echo "🔑 PAT configured: ${PAT_TOKEN:0:10}..."
echo "🌐 NGROK_URL: $NGROK_URL"
echo ""

# Start the bot
dotnet run

echo ""
echo "✅ Bot process ended!"
