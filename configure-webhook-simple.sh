#!/bin/bash

# Simple Webhook Configuration for Code Review Bot
# This script creates webhooks for Azure DevOps pull request events

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
NC='\033[0m' # No Color

# Function to print colored output
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

print_step() {
    echo -e "${WHITE}📋 $1${NC}"
}

# Check if required tools are installed
check_prerequisites() {
    if ! command -v curl &> /dev/null; then
        print_error "curl is required but not installed."
        exit 1
    fi

    if ! command -v jq &> /dev/null; then
        print_warning "jq is not installed. JSON responses will not be formatted."
        print_info "Install jq with: brew install jq (macOS) or apt-get install jq (Ubuntu)"
    fi
}

# Function to create webhook subscription
create_webhook_subscription() {
    local event_type="$1"
    local description="$2"
    
    print_info "Creating webhook for: $description"
    
    # Debug: Show what we're sending
    echo "Debug: Sending webhook to: $ORGANIZATION_URL/_apis/hooks/subscriptions?api-version=6.0"
    echo "Debug: Project ID: $PROJECT_NAME"
    echo "Debug: Webhook URL: $WEBHOOK_URL"
    
    # Create the webhook payload
    local webhook_payload=$(cat <<EOF
{
    "publisherId": "tfs",
    "eventType": "$event_type",
    "resourceVersion": "1.0",
    "consumerId": "webHooks",
    "consumerActionId": "httpRequest",
    "publisherInputs": {
        "projectId": "$PROJECT_NAME"
    },
    "consumerInputs": {
        "url": "$WEBHOOK_URL",
        "httpHeaders": {
            "Content-Type": "application/json"
        }
    },
    "resourceFilters": []
}
EOF
)
    
    # Make the API call
    local response=$(curl -s -w "\n%{http_code}" \
        -X POST \
        -H "Authorization: Basic $AUTH_HEADER" \
        -H "Content-Type: application/json" \
        -d "$webhook_payload" \
        "$ORGANIZATION_URL/_apis/hooks/subscriptions?api-version=6.0")
    
    local http_code=$(echo "$response" | tail -n1)
    local response_body=$(echo "$response" | sed '$d')
    
    if [ "$http_code" -eq 200 ] || [ "$http_code" -eq 201 ]; then
        if command -v jq &> /dev/null; then
            local webhook_id=$(echo "$response_body" | jq -r '.id')
            print_status "$description webhook created successfully with ID: $webhook_id"
        else
            print_status "$description webhook created successfully"
        fi
        return 0
    else
        print_error "Failed to create $description webhook (HTTP $http_code)"
        echo "Response: $response_body"
        
        # Additional debugging for common issues
        if [ "$http_code" -eq 400 ]; then
            echo ""
            print_warning "Common causes of HTTP 400 errors:"
            echo "  • Invalid organization URL format"
            echo "  • Project name not found or incorrect"
            echo "  • Personal Access Token doesn't have required permissions"
            echo "  • Webhook URL is not accessible"
            echo ""
            print_info "Debugging information:"
            echo "  • Organization URL: $ORGANIZATION_URL"
            echo "  • Project Name: $PROJECT_NAME"
            echo "  • Webhook URL: $WEBHOOK_URL"
            echo "  • Event Type: $event_type"
        fi
        
        return 1
    fi
}

# Main script
main() {
    echo -e "${GREEN}🤖 Configuring webhook for Code Review Bot...${NC}"
    echo ""
    
    # Check prerequisites
    check_prerequisites
    
    # Get parameters
    if [ $# -lt 4 ]; then
        echo "Usage: $0 <organization_url> <project_name> <personal_access_token> <bot_service_url>"
        echo ""
        echo "Example:"
        echo "  $0 \"https://dev.azure.com/mycompany\" \"MyProject\" \"abc123...\" \"https://abc123.ngrok-free.app\""
        exit 1
    fi
    
    ORGANIZATION_URL="$1"
    PROJECT_NAME="$2"
    PERSONAL_ACCESS_TOKEN="$3"
    BOT_SERVICE_URL="$4"
    
    # Bot webhook URL
    WEBHOOK_URL="$BOT_SERVICE_URL/api/webhook"
    
    print_info "Organization: $ORGANIZATION_URL"
    print_info "Project: $PROJECT_NAME"
    print_info "Bot URL: $BOT_SERVICE_URL"
    print_info "Webhook Endpoint: $WEBHOOK_URL"
    echo ""
    
    # Create authorization header
    AUTH_HEADER=$(echo -n ":$PERSONAL_ACCESS_TOKEN" | base64)
    
    # Test the bot service first
    print_step "Testing bot service connectivity..."
    if curl -s "$BOT_SERVICE_URL/api/webhook/health" > /dev/null; then
        print_status "Bot service is accessible"
    else
        print_warning "Bot service may not be accessible at $BOT_SERVICE_URL"
        print_info "Make sure your bot is running and ngrok tunnel is active"
    fi
    echo ""
    
    # Create webhooks for pull request events
    print_step "Creating webhook subscriptions..."
    echo ""
    
    local success_count=0
    local total_count=2
    
    if create_webhook_subscription "git.pullrequest.created" "Pull Request Created"; then
        ((success_count++))
    fi
    
    if create_webhook_subscription "git.pullrequest.updated" "Pull Request Updated"; then
        ((success_count++))
    fi
    
    echo ""
    print_step "Webhook configuration completed!"
    echo ""
    
    print_info "Summary:"
    echo -e "${WHITE}  • Bot Service URL: $BOT_SERVICE_URL${NC}"
    echo -e "${WHITE}  • Webhook Endpoint: $WEBHOOK_URL${NC}"
    echo -e "${WHITE}  • Organization: $ORGANIZATION_URL${NC}"
    echo -e "${WHITE}  • Project: $PROJECT_NAME${NC}"
    echo -e "${WHITE}  • Webhooks Created: $success_count/$total_count${NC}"
    
    if [ $success_count -eq $total_count ]; then
        echo ""
        print_status "All webhooks created successfully!"
        print_info "You can now create pull requests to test the integration."
    else
        echo ""
        print_warning "Some webhooks may not have been created. Check the errors above."
    fi
    
    echo ""
    print_info "To test:"
    echo -e "${WHITE}  1. Create a pull request in your Azure DevOps project${NC}"
    echo -e "${WHITE}  2. Check your bot logs for webhook events${NC}"
    echo -e "${WHITE}  3. Verify webhook delivery in Azure DevOps Service Hooks${NC}"
    echo ""
}

# Run the main function
main "$@"
