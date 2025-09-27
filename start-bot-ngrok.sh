#!/bin/bash

# Start Code Review Bot with ngrok support
# This script sets up the environment to work properly with ngrok

echo "🚀 Starting Code Review Bot with ngrok support..."

# Set environment variable to indicate we're behind a proxy
export NGROK_URL="https://ngrok.io"

# Navigate to the presentation project
cd src/CodeReviewBot.Presentation

# Start the bot
echo "Starting bot on http://localhost:5002..."
dotnet run

echo "✅ Bot started successfully!"
echo ""
echo "💡 Tips:"
echo "- The bot is now running on http://localhost:5002"
echo "- HTTPS redirection is disabled for ngrok compatibility"
echo "- Start ngrok in another terminal: ngrok http 5002"
echo "- Use the ngrok HTTPS URL for your webhook configuration"
