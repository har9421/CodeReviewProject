#!/bin/bash

# Manual Webhook Configuration Script
# This script provides step-by-step instructions for manual webhook setup

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
BLUE='\033[0;34m'
NC='\033[0m'

print_header() {
    echo -e "${BLUE}$1${NC}"
}

print_step() {
    echo -e "${WHITE}$1${NC}"
}

print_info() {
    echo -e "${CYAN}$1${NC}"
}

print_success() {
    echo -e "${GREEN}$1${NC}"
}

print_warning() {
    echo -e "${YELLOW}$1${NC}"
}

print_error() {
    echo -e "${RED}$1${NC}"
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
    
    local org_url="$1"
    local project_name="$2"
    local pat="$3"
    local bot_url="$4"
    
    print_header "🤖 Manual Webhook Configuration for Code Review Bot"
    echo "=================================================="
    echo ""
    
    print_info "Your Configuration:"
    echo "  • Organization: $org_url"
    echo "  • Project: $project_name"
    echo "  • Bot URL: $bot_url"
    echo "  • Webhook Endpoint: $bot_url/api/webhook"
    echo ""
    
    # Test bot connectivity first
    print_step "Step 1: Testing bot connectivity..."
    if curl -s "$bot_url/api/webhook/health" > /dev/null; then
        print_success "✅ Bot service is accessible"
    else
        print_error "❌ Bot service is not accessible"
        print_warning "Make sure your bot is running and ngrok tunnel is active"
        exit 1
    fi
    echo ""
    
    # Test Azure DevOps connectivity
    print_step "Step 2: Testing Azure DevOps connectivity..."
    local auth_header=$(echo -n ":$pat" | base64)
    local api_url="$org_url/_apis/projects?api-version=6.0"
    
    local response=$(curl -s -w "\n%{http_code}" \
        -H "Authorization: Basic $auth_header" \
        "$api_url")
    
    local http_code=$(echo "$response" | tail -n1)
    local response_body=$(echo "$response" | sed '$d')
    
    if [ "$http_code" -eq 200 ]; then
        print_success "✅ Azure DevOps API is accessible"
        
        # Check if project exists
        if echo "$response_body" | grep -q "\"name\":\s*\"$project_name\""; then
            print_success "✅ Project '$project_name' found"
        else
            print_warning "⚠️  Project '$project_name' not found"
            print_info "Available projects:"
            echo "$response_body" | grep -o '"name":"[^"]*"' | sed 's/"name":"//g' | sed 's/"//g' | sed 's/^/  • /'
            echo ""
            print_warning "Please use the exact project name from the list above"
            exit 1
        fi
    else
        print_error "❌ Azure DevOps API returned HTTP $http_code"
        print_warning "Check your Personal Access Token and organization URL"
        exit 1
    fi
    echo ""
    
    # Manual configuration instructions
    print_step "Step 3: Manual Webhook Configuration"
    echo ""
    print_info "Since automated configuration is having issues, let's set it up manually:"
    echo ""
    
    echo "1. Open your browser and go to:"
    print_info "   $org_url/$project_name/_settings/serviceHooks"
    echo ""
    
    echo "2. Click 'Create subscription'"
    echo ""
    
    echo "3. Select 'Web Hooks' and click 'Next'"
    echo ""
    
    echo "4. Select these events:"
    echo "   ✅ Pull request created"
    echo "   ✅ Pull request updated"
    echo "   Click 'Next'"
    echo ""
    
    echo "5. Configure the webhook:"
    echo "   URL: $bot_url/api/webhook"
    echo "   Click 'Test' (should return 200 OK)"
    echo "   Click 'Finish'"
    echo ""
    
    print_success "🎉 Webhook configuration completed!"
    echo ""
    
    print_info "Testing Instructions:"
    echo "1. Create a test pull request in your project"
    echo "2. Check your bot logs for webhook events:"
    echo "   tail -f logs/codereviewbot-*.log"
    echo "3. Verify the bot posts comments on your PR"
    echo ""
    
    print_info "Troubleshooting:"
    echo "• If webhook test fails, check that your bot is running"
    echo "• If no comments appear, check bot logs for errors"
    echo "• Make sure your PAT has 'Pull Requests (Read & Write)' permissions"
    echo ""
    
    # Open browser automatically (if possible)
    if command -v open &> /dev/null; then
        print_info "Opening Azure DevOps Service Hooks page..."
        open "$org_url/$project_name/_settings/serviceHooks"
    elif command -v xdg-open &> /dev/null; then
        print_info "Opening Azure DevOps Service Hooks page..."
        xdg-open "$org_url/$project_name/_settings/serviceHooks"
    else
        print_info "Please manually navigate to the Service Hooks page"
    fi
}

main "$@"
