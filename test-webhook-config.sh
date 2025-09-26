#!/bin/bash

# Test script to debug webhook configuration issues

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
NC='\033[0m'

print_status() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_info() {
    echo -e "${CYAN}🔗 $1${NC}"
}

# Test function
test_webhook_config() {
    local org_url="$1"
    local project_name="$2"
    local pat="$3"
    local bot_url="$4"
    
    echo "Testing webhook configuration..."
    echo ""
    
    # Test 1: Check organization URL format
    print_info "Test 1: Organization URL format"
    if [[ "$org_url" =~ ^https://dev\.azure\.com/[^/]+/?$ ]]; then
        print_status "Organization URL format looks correct"
    else
        print_error "Organization URL format may be incorrect"
        print_warning "Expected format: https://dev.azure.com/YOUR_ORG"
        print_warning "Got: $org_url"
    fi
    echo ""
    
    # Test 2: Test Azure DevOps API connectivity
    print_info "Test 2: Azure DevOps API connectivity"
    local auth_header=$(echo -n ":$pat" | base64)
    local api_url="$org_url/_apis/projects?api-version=6.0"
    
    local response=$(curl -s -w "\n%{http_code}" \
        -H "Authorization: Basic $auth_header" \
        "$api_url")
    
    local http_code=$(echo "$response" | tail -n1)
    local response_body=$(echo "$response" | sed '$d')
    
    if [ "$http_code" -eq 200 ]; then
        print_status "Azure DevOps API is accessible"
        
        # Check if project exists
        if echo "$response_body" | grep -q "\"name\":\s*\"$project_name\""; then
            print_status "Project '$project_name' found in organization"
        else
            print_warning "Project '$project_name' not found in organization"
            print_info "Available projects:"
            echo "$response_body" | grep -o '"name":"[^"]*"' | sed 's/"name":"//g' | sed 's/"//g' | sed 's/^/  • /'
        fi
    else
        print_error "Azure DevOps API returned HTTP $http_code"
        print_warning "Check your Personal Access Token and organization URL"
        echo "Response: $response_body"
    fi
    echo ""
    
    # Test 3: Test bot service connectivity
    print_info "Test 3: Bot service connectivity"
    local health_url="$bot_url/api/webhook/health"
    
    local bot_response=$(curl -s -w "\n%{http_code}" "$health_url")
    local bot_http_code=$(echo "$bot_response" | tail -n1)
    local bot_response_body=$(echo "$bot_response" | sed '$d')
    
    if [ "$bot_http_code" -eq 200 ]; then
        print_status "Bot service is accessible"
        echo "Response: $bot_response_body"
    else
        print_error "Bot service returned HTTP $bot_http_code"
        print_warning "Make sure your bot is running and ngrok tunnel is active"
        echo "Response: $bot_response_body"
    fi
    echo ""
    
    # Test 4: Test webhook payload
    print_info "Test 4: Webhook payload validation"
    local webhook_payload=$(cat <<EOF
{
    "publisherId": "tfs",
    "eventType": "git.pullrequest.created",
    "resourceVersion": "1.0",
    "consumerId": "webHooks",
    "consumerActionId": "httpRequest",
    "publisherInputs": {
        "projectId": "$project_name"
    },
    "consumerInputs": {
        "url": "$bot_url/api/webhook",
        "httpHeaders": {
            "Content-Type": "application/json"
        }
    }
}
EOF
)
    
    print_status "Webhook payload created successfully"
    echo "Payload preview:"
    echo "$webhook_payload" | head -n 10
    echo "..."
    echo ""
    
    # Test 5: Try to create webhook (dry run)
    print_info "Test 5: Attempting to create webhook (this will actually create it)"
    local webhook_url="$org_url/_apis/hooks/subscriptions?api-version=6.0"
    
    local webhook_response=$(curl -s -w "\n%{http_code}" \
        -X POST \
        -H "Authorization: Basic $auth_header" \
        -H "Content-Type: application/json" \
        -d "$webhook_payload" \
        "$webhook_url")
    
    local webhook_http_code=$(echo "$webhook_response" | tail -n1)
    local webhook_response_body=$(echo "$webhook_response" | sed '$d')
    
    if [ "$webhook_http_code" -eq 200 ] || [ "$webhook_http_code" -eq 201 ]; then
        print_status "Webhook created successfully!"
        if command -v jq &> /dev/null; then
            local webhook_id=$(echo "$webhook_response_body" | jq -r '.id')
            echo "Webhook ID: $webhook_id"
        fi
    else
        print_error "Webhook creation failed with HTTP $webhook_http_code"
        echo "Response: $webhook_response_body"
        
        # Parse common error messages
        if echo "$webhook_response_body" | grep -q "Project.*not found"; then
            print_error "Project '$project_name' not found in organization"
        elif echo "$webhook_response_body" | grep -q "unauthorized\|Unauthorized"; then
            print_error "Authentication failed - check your Personal Access Token"
        elif echo "$webhook_response_body" | grep -q "url.*invalid\|URL.*invalid"; then
            print_error "Webhook URL is invalid or not accessible"
        fi
    fi
}

# Main function
main() {
    if [ $# -ne 4 ]; then
        echo "Usage: $0 <organization_url> <project_name> <personal_access_token> <bot_service_url>"
        echo ""
        echo "Example:"
        echo "  $0 \"https://dev.azure.com/mycompany\" \"MyProject\" \"abc123...\" \"https://abc123.ngrok-free.app\""
        exit 1
    fi
    
    echo -e "${GREEN}🧪 Webhook Configuration Test${NC}"
    echo "================================"
    echo ""
    
    test_webhook_config "$1" "$2" "$3" "$4"
    
    echo ""
    echo -e "${GREEN}Test completed!${NC}"
}

main "$@"
