#!/bin/bash

# Start Code Review Bot with PAT environment variable
# This script ensures the PAT is properly set before starting the bot

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${GREEN}🤖 Starting Code Review Bot with PAT...${NC}"

# Check if PAT is set
if [ -z "$AZURE_DEVOPS_PAT" ]; then
    echo -e "${RED}❌ AZURE_DEVOPS_PAT environment variable is not set${NC}"
    echo "Please set it first:"
    echo "export AZURE_DEVOPS_PAT=\"your-token-here\""
    exit 1
fi

echo -e "${GREEN}✅ PAT is set (${#AZURE_DEVOPS_PAT} characters)${NC}"

# Kill any existing bot processes
echo -e "${YELLOW}🔄 Stopping any existing bot processes...${NC}"
lsof -ti:5000 | xargs kill -9 2>/dev/null || true

# Wait a moment
sleep 2

# Start the bot
echo -e "${GREEN}🚀 Starting the bot...${NC}"
cd /Users/code/CodeReviewProject/src/CodeReviewBot

# Export the PAT again to ensure it's available
export AZURE_DEVOPS_PAT="$AZURE_DEVOPS_PAT"

# Start the bot in the background
dotnet run --urls="http://localhost:5000" &

# Get the process ID
BOT_PID=$!

# Wait a moment for the bot to start
sleep 5

# Check if the bot is running
if curl -s http://localhost:5000/api/webhook/health > /dev/null; then
    echo -e "${GREEN}✅ Bot is running successfully on http://localhost:5000${NC}"
    echo -e "${GREEN}✅ Process ID: $BOT_PID${NC}"
    echo -e "${GREEN}✅ PAT is available to the bot process${NC}"
    
    # Show how to stop the bot
    echo ""
    echo -e "${YELLOW}To stop the bot, run: kill $BOT_PID${NC}"
    echo -e "${YELLOW}Or use: lsof -ti:5000 | xargs kill -9${NC}"
    
    # Show logs
    echo ""
    echo -e "${GREEN}📊 Bot logs:${NC}"
    echo "tail -f logs/codereviewbot-*.log"
    
else
    echo -e "${RED}❌ Bot failed to start${NC}"
    exit 1
fi
