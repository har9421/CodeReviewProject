#!/bin/bash

# Stop All Code Review Bot Processes Script

echo "🛑 Stopping All Code Review Bot Processes..."
echo "============================================="

# Kill all bot-related processes
echo "🛑 Stopping CodeReviewBot processes..."
BOT_COUNT=$(pkill -f "CodeReviewBot.Presentation" 2>/dev/null && echo "stopped" || echo "none")
echo "   CodeReviewBot processes: $BOT_COUNT"

echo "🛑 Stopping dotnet run processes..."
DOTNET_COUNT=$(pkill -f "dotnet run" 2>/dev/null && echo "stopped" || echo "none")
echo "   dotnet run processes: $DOTNET_COUNT"

echo "🛑 Stopping ngrok processes..."
NGROK_COUNT=$(pkill -f "ngrok" 2>/dev/null && echo "stopped" || echo "none")
echo "   ngrok processes: $NGROK_COUNT"

echo "🛑 Stopping processes on port 5002..."
PORT_COUNT=$(lsof -ti:5002 | xargs kill -9 2>/dev/null && echo "stopped" || echo "none")
echo "   Port 5002 processes: $PORT_COUNT"

echo "🛑 Stopping processes on port 5000..."
PORT_COUNT=$(lsof -ti:5000 | xargs kill -9 2>/dev/null && echo "stopped" || echo "none")
echo "   Port 5000 processes: $PORT_COUNT"

echo ""
echo "✅ All processes stopped successfully!"
echo ""
echo "📋 Verification:"
echo "================="

# Check if any processes are still running
REMAINING=$(ps aux | grep -E "(CodeReviewBot|dotnet run|ngrok)" | grep -v grep | wc -l)

if [ "$REMAINING" -eq 0 ]; then
    echo "✅ No bot-related processes are running"
else
    echo "⚠️  $REMAINING processes may still be running:"
    ps aux | grep -E "(CodeReviewBot|dotnet run|ngrok)" | grep -v grep
fi

echo ""
echo "🎯 All Code Review Bot processes have been stopped!"
echo "   Run ./complete-setup.sh to start everything again."
