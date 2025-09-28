#!/bin/bash

# Test Azure DevOps PAT Setup
echo "🔍 Testing Azure DevOps PAT Configuration..."
echo ""

# Check if PAT is set
if [ -z "$AZURE_DEVOPS_PAT" ]; then
    echo "❌ AZURE_DEVOPS_PAT environment variable is not set"
    echo ""
    echo "To set it:"
    echo "export AZURE_DEVOPS_PAT=\"your-pat-token-here\""
    echo ""
    echo "Or use the startup script:"
    echo "./start-bot-with-pat.sh \"your-pat-token-here\""
    exit 1
fi

echo "✅ PAT is set: ${AZURE_DEVOPS_PAT:0:10}..."
echo ""

# Test PAT with Azure DevOps API
echo "🧪 Testing PAT with Azure DevOps API..."

# Get your organization info
ORG_URL="https://dev.azure.com/khUniverse"
PROJECTS_URL="${ORG_URL}/_apis/projects?api-version=7.0"

echo "Testing connection to: $PROJECTS_URL"

# Make API call
RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" \
    -H "Authorization: Basic $(echo -n :$AZURE_DEVOPS_PAT | base64)" \
    -H "Content-Type: application/json" \
    "$PROJECTS_URL")

# Extract HTTP status
HTTP_STATUS=$(echo "$RESPONSE" | grep "HTTP_STATUS:" | cut -d: -f2)
RESPONSE_BODY=$(echo "$RESPONSE" | sed '/HTTP_STATUS:/d')

echo "HTTP Status: $HTTP_STATUS"
echo ""

if [ "$HTTP_STATUS" = "200" ]; then
    echo "✅ PAT is working correctly!"
    echo "✅ Successfully connected to Azure DevOps"
    echo ""
    
    # Try to extract project count
    PROJECT_COUNT=$(echo "$RESPONSE_BODY" | grep -o '"count":[0-9]*' | cut -d: -f2)
    if [ ! -z "$PROJECT_COUNT" ]; then
        echo "📊 Found $PROJECT_COUNT project(s) in your organization"
    fi
    
    echo ""
    echo "🎉 Your PAT is ready for the Code Review Bot!"
    echo ""
    echo "Next steps:"
    echo "1. Start your bot: ./start-bot-with-pat.sh \"$AZURE_DEVOPS_PAT\""
    echo "2. Start ngrok: ngrok http 5002"
    echo "3. Configure webhook with ngrok URL"
    echo "4. Create a test pull request"
    
elif [ "$HTTP_STATUS" = "401" ]; then
    echo "❌ PAT authentication failed (401 Unauthorized)"
    echo ""
    echo "Possible issues:"
    echo "- PAT token is incorrect or expired"
    echo "- PAT doesn't have required permissions"
    echo "- Organization URL is incorrect"
    echo ""
    echo "Solution:"
    echo "1. Go to: https://dev.azure.com/khUniverse/_usersSettings/tokens"
    echo "2. Create a new PAT with 'Code (read & write)' permissions"
    echo "3. Copy the new token and set it again"
    
elif [ "$HTTP_STATUS" = "403" ]; then
    echo "❌ PAT access forbidden (403 Forbidden)"
    echo ""
    echo "Possible issues:"
    echo "- PAT doesn't have required scopes"
    echo "- PAT doesn't have access to your organization"
    echo ""
    echo "Solution:"
    echo "1. Edit your PAT: https://dev.azure.com/khUniverse/_usersSettings/tokens"
    echo "2. Add 'Code (read & write)' and 'Pull Requests (read & write)' scopes"
    echo "3. Save the changes"
    
else
    echo "❌ Unexpected response (HTTP $HTTP_STATUS)"
    echo ""
    echo "Response: $RESPONSE_BODY"
    echo ""
    echo "Please check:"
    echo "- Your internet connection"
    echo "- Azure DevOps service status"
    echo "- PAT token validity"
fi

echo ""
echo "🔧 Debug Info:"
echo "Organization: khUniverse"
echo "PAT (first 10 chars): ${AZURE_DEVOPS_PAT:0:10}..."
echo "API Endpoint: $PROJECTS_URL"
