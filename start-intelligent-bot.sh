#!/bin/bash

# Intelligent Code Review Bot Startup Script
# This script starts the bot with all intelligent features enabled

echo "🚀 Starting Intelligent Code Review Bot..."

# Check if PAT is set
if [ -z "$AZURE_DEVOPS_PAT" ]; then
    echo "❌ Error: AZURE_DEVOPS_PAT environment variable is not set"
    echo "Please set your Personal Access Token:"
    echo "export AZURE_DEVOPS_PAT='your-pat-here'"
    exit 1
fi

# Create necessary directories
mkdir -p logs
mkdir -p learning-data

# Set environment variables for intelligent features
export ENABLE_LEARNING=true
export ENABLE_PERFORMANCE_MONITORING=true
export ENABLE_INTELLIGENT_FILTERING=true

echo "✅ Environment configured for intelligent features"
echo "📊 Learning system: Enabled"
echo "⚡ Performance monitoring: Enabled"
echo "🎯 Intelligent filtering: Enabled"

# Start the bot
echo "🔧 Starting bot with intelligent features..."
cd src/CodeReviewBot.Presentation
dotnet run --urls "http://localhost:5002;https://localhost:5003"

echo "🤖 Intelligent Code Review Bot is running!"
echo "📡 Webhook URL: http://localhost:5002/api/webhook"
echo "📊 Performance API: http://localhost:5002/api/performance"
echo "🧠 Learning API: http://localhost:5002/api/feedback"
echo "💡 Health Check: http://localhost:5002/api/performance/health"
