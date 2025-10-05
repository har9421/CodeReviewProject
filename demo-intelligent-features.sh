#!/bin/bash

# Intelligent Code Review Bot - Feature Demonstration Script
# This script demonstrates the new intelligent features

BOT_URL="http://localhost:5002"

echo "🎯 Intelligent Code Review Bot - Feature Demonstration"
echo "======================================================"

# Function to make API calls with error handling
api_call() {
    local method=$1
    local endpoint=$2
    local data=$3
    
    if [ -n "$data" ]; then
        curl -s -X $method "$BOT_URL$endpoint" \
             -H "Content-Type: application/json" \
             -d "$data"
    else
        curl -s -X $method "$BOT_URL$endpoint"
    fi
}

echo ""
echo "1. 🏥 Checking Bot Health..."
echo "----------------------------"
api_call "GET" "/api/performance/health" | jq '.'

echo ""
echo "2. 📊 Getting Performance Report..."
echo "----------------------------------"
api_call "GET" "/api/performance/report" | jq '.'

echo ""
echo "3. 🧠 Getting Learning Insights..."
echo "---------------------------------"
api_call "GET" "/api/feedback/insights" | jq '.'

echo ""
echo "4. 📈 Getting Rule Effectiveness..."
echo "----------------------------------"
api_call "GET" "/api/feedback/rule-effectiveness" | jq '.'

echo ""
echo "5. 🔧 Triggering Rule Optimization..."
echo "-------------------------------------"
api_call "POST" "/api/feedback/optimize-rules" | jq '.'

echo ""
echo "6. 📝 Submitting Sample Feedback..."
echo "----------------------------------"
# Submit feedback for a sample issue
feedback_data='{
  "issueId": "demo-issue-123",
  "ruleId": "no-console-writeline",
  "filePath": "src/Program.cs",
  "lineNumber": 15,
  "feedbackType": "Accepted",
  "comment": "Good catch! Will fix this Console.WriteLine"
}'

api_call "POST" "/api/feedback/issue-feedback" "$feedback_data" | jq '.'

echo ""
echo "7. 📊 Getting Updated Performance Metrics..."
echo "-------------------------------------------"
api_call "GET" "/api/performance/report" | jq '.Summary'

echo ""
echo "8. 🚨 Checking for Performance Alerts..."
echo "---------------------------------------"
api_call "GET" "/api/performance/alerts" | jq '.'

echo ""
echo "✅ Demonstration Complete!"
echo ""
echo "🔗 Available Endpoints:"
echo "  - Health Check: $BOT_URL/api/performance/health"
echo "  - Performance: $BOT_URL/api/performance/report"
echo "  - Learning: $BOT_URL/api/feedback/insights"
echo "  - Webhook: $BOT_URL/api/webhook"
echo ""
echo "📚 For more information, see INTELLIGENT-FEATURES.md"
