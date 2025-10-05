#!/bin/bash

# Intelligent Code Review Bot - Complete Setup & Learning Script
# This script sets up the intelligent bot and feeds it millions of C# PRs for learning

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Configuration
BOT_URL="https://localhost:5003"
GITHUB_TOKEN="${GITHUB_TOKEN:-}"
INGESTION_MODE="${INGESTION_MODE:-large}"  # small, medium, large, massive
AUTO_START_INGESTION="${AUTO_START_INGESTION:-true}"

# Function to print colored output
print_status() {
    local color=$1
    local message=$2
    echo -e "${color}[$(date '+%H:%M:%S')] ${message}${NC}"
}

print_header() {
    echo -e "${PURPLE}"
    echo "╔══════════════════════════════════════════════════════════════════════════════╗"
    echo "║                    Intelligent Code Review Bot Setup & Learning              ║"
    echo "║                           Million C# PRs Ingestion                          ║"
    echo "╚══════════════════════════════════════════════════════════════════════════════╝"
    echo -e "${NC}"
}

print_step() {
    local step=$1
    local message=$2
    echo -e "${CYAN}Step $step: $message${NC}"
}

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to check prerequisites
check_prerequisites() {
    print_step "1" "Checking prerequisites..."
    
    local missing_deps=()
    
    # Check for required commands
    if ! command_exists dotnet; then
        missing_deps+=("dotnet")
    fi
    
    if ! command_exists curl; then
        missing_deps+=("curl")
    fi
    
    if ! command_exists jq; then
        missing_deps+=("jq")
    fi
    
    if ! command_exists git; then
        missing_deps+=("git")
    fi
    
    if [ ${#missing_deps[@]} -ne 0 ]; then
        print_status $RED "Missing required dependencies: ${missing_deps[*]}"
        echo ""
        echo "Please install missing dependencies:"
        echo "  macOS: brew install dotnet curl jq git"
        echo "  Ubuntu: sudo apt-get install dotnet-sdk-8.0 curl jq git"
        echo "  Windows: choco install dotnet curl jq git"
        exit 1
    fi
    
    # Check for GitHub token
    if [ -z "$GITHUB_TOKEN" ]; then
        print_status $YELLOW "GitHub token not found. Please set GITHUB_TOKEN environment variable."
        echo ""
        echo "Get your token from: https://github.com/settings/tokens"
        echo "Required scopes: repo (read access to repositories)"
        echo ""
        read -p "Enter your GitHub token: " GITHUB_TOKEN
        export GITHUB_TOKEN
    fi
    
    # Validate GitHub token
    print_status $BLUE "Validating GitHub token..."
    local token_valid=$(curl -s -H "Authorization: token $GITHUB_TOKEN" https://api.github.com/user | jq -r '.login // empty')
    if [ -z "$token_valid" ]; then
        print_status $RED "Invalid GitHub token. Please check your token and try again."
        exit 1
    fi
    print_status $GREEN "GitHub token validated for user: $token_valid"
    
    print_status $GREEN "✅ All prerequisites met"
}

# Function to build the project
build_project() {
    print_step "2" "Building the intelligent bot project..."
    
    # Restore packages
    print_status $BLUE "Restoring NuGet packages..."
    dotnet restore
    
    # Build the project
    print_status $BLUE "Building project..."
    dotnet build --configuration Release --no-restore
    
    print_status $GREEN "✅ Project built successfully"
}

# Function to create necessary directories
setup_directories() {
    print_step "3" "Setting up directories..."
    
    mkdir -p logs
    mkdir -p learning-data
    mkdir -p ingestion-data
    
    print_status $GREEN "✅ Directories created"
}

# Function to start the bot
start_bot() {
    print_step "4" "Starting the intelligent bot..."
    
    # Kill any existing bot processes
    print_status $BLUE "Stopping any existing bot processes..."
    pkill -f "CodeReviewBot" || true
    sleep 2
    
    # Start the bot in background
    print_status $BLUE "Starting bot with intelligent features..."
    cd src/CodeReviewBot.Presentation
    
    # Set environment variables for intelligent features
    export ENABLE_LEARNING=true
    export ENABLE_PERFORMANCE_MONITORING=true
    export ENABLE_INTELLIGENT_FILTERING=true
    export GITHUB_TOKEN="$GITHUB_TOKEN"
    
    # Start bot in background
    nohup dotnet run --configuration Release --urls "http://localhost:5002;https://localhost:5003" > ../../logs/bot.log 2>&1 &
    local bot_pid=$!
    
    cd ../..
    
    # Wait for bot to start
    print_status $BLUE "Waiting for bot to start..."
    local max_attempts=30
    local attempt=0
    
    while [ $attempt -lt $max_attempts ]; do
        if curl -k -s "$BOT_URL/api/performance/report" > /dev/null 2>&1; then
            print_status $GREEN "✅ Bot started successfully (PID: $bot_pid)"
            echo $bot_pid > bot.pid
            return 0
        fi
        
        sleep 2
        attempt=$((attempt + 1))
        print_status $YELLOW "Attempt $attempt/$max_attempts - waiting for bot to start..."
    done
    
    print_status $RED "❌ Failed to start bot. Check logs/bot.log for details."
    exit 1
}

# Function to wait for bot to be ready
wait_for_bot_ready() {
    print_step "5" "Waiting for bot to be ready..."
    
    local max_attempts=60
    local attempt=0
    
    while [ $attempt -lt $max_attempts ]; do
        local health_status=$(curl -k -s "$BOT_URL/api/performance/report" | jq -r '.summary.totalExecutions // "unknown"')
        
        if [ "$health_status" != "unknown" ]; then
            print_status $GREEN "✅ Bot is ready and healthy"
            return 0
        fi
        
        sleep 5
        attempt=$((attempt + 1))
        print_status $YELLOW "Attempt $attempt/$max_attempts - Bot status: $health_status"
    done
    
    print_status $YELLOW "⚠️  Bot may not be fully ready, but continuing..."
}

# Function to configure ingestion based on mode
configure_ingestion() {
    print_step "6" "Configuring data ingestion mode: $INGESTION_MODE"
    
    case $INGESTION_MODE in
        "small")
            INGESTION_CONFIG='{
                "usePopularRepositories": true,
                "customRepositories": [
                    "microsoft/dotnet",
                    "dotnet/core",
                    "dotnet/aspnetcore"
                ],
                "maxRepositories": 10,
                "maxPRsPerRepository": 100,
                "startDate": "2023-01-01T00:00:00Z",
                "endDate": "2023-12-31T23:59:59Z",
                "languages": ["csharp"]
            }'
            ESTIMATED_DURATION="10-15 minutes"
            ;;
        "medium")
            INGESTION_CONFIG='{
                "usePopularRepositories": true,
                "customRepositories": [
                    "microsoft/dotnet",
                    "dotnet/core",
                    "dotnet/aspnetcore",
                    "dotnet/efcore",
                    "dotnet/runtime",
                    "microsoft/vscode",
                    "PowerShell/PowerShell"
                ],
                "maxRepositories": 50,
                "maxPRsPerRepository": 500,
                "startDate": "2022-01-01T00:00:00Z",
                "endDate": "2023-12-31T23:59:59Z",
                "languages": ["csharp"]
            }'
            ESTIMATED_DURATION="30-45 minutes"
            ;;
        "large")
            INGESTION_CONFIG='{
                "usePopularRepositories": true,
                "customRepositories": [
                    "microsoft/dotnet",
                    "dotnet/core",
                    "dotnet/aspnetcore",
                    "dotnet/efcore",
                    "dotnet/runtime",
                    "microsoft/vscode",
                    "PowerShell/PowerShell",
                    "microsoft/TypeScript",
                    "microsoft/monaco-editor",
                    "microsoft/ApplicationInsights-dotnet",
                    "microsoft/azure-powershell",
                    "microsoft/azure-sdk-for-net",
                    "microsoft/azure-functions-host",
                    "microsoft/azure-webjobs-sdk",
                    "microsoft/azure-storage-net",
                    "microsoft/azure-cosmos-dotnet-v3",
                    "microsoft/azure-service-bus-dotnet",
                    "microsoft/azure-keyvault-net",
                    "microsoft/azure-identity-dotnet"
                ],
                "maxRepositories": 200,
                "maxPRsPerRepository": 1000,
                "startDate": "2021-01-01T00:00:00Z",
                "endDate": "2023-12-31T23:59:59Z",
                "languages": ["csharp"]
            }'
            ESTIMATED_DURATION="1-2 hours"
            ;;
        "massive")
            INGESTION_CONFIG='{
                "usePopularRepositories": true,
                "customRepositories": [
                    "microsoft/dotnet",
                    "dotnet/core",
                    "dotnet/aspnetcore",
                    "dotnet/efcore",
                    "dotnet/runtime",
                    "microsoft/vscode",
                    "PowerShell/PowerShell",
                    "microsoft/TypeScript",
                    "microsoft/monaco-editor",
                    "microsoft/ApplicationInsights-dotnet",
                    "microsoft/azure-powershell",
                    "microsoft/azure-sdk-for-net",
                    "microsoft/azure-functions-host",
                    "microsoft/azure-webjobs-sdk",
                    "microsoft/azure-storage-net",
                    "microsoft/azure-cosmos-dotnet-v3",
                    "microsoft/azure-service-bus-dotnet",
                    "microsoft/azure-keyvault-net",
                    "microsoft/azure-identity-dotnet",
                    "microsoft/azure-storage-blobs-dotnet",
                    "microsoft/azure-storage-queues-dotnet",
                    "microsoft/azure-storage-files-dotnet",
                    "microsoft/azure-storage-tables-dotnet"
                ],
                "maxRepositories": 1000,
                "maxPRsPerRepository": 1000,
                "startDate": "2020-01-01T00:00:00Z",
                "endDate": "2024-01-01T00:00:00Z",
                "languages": ["csharp"]
            }'
            ESTIMATED_DURATION="3-4 hours"
            ;;
        *)
            print_status $RED "Invalid ingestion mode: $INGESTION_MODE"
            print_status $YELLOW "Valid modes: small, medium, large, massive"
            exit 1
            ;;
    esac
    
    print_status $GREEN "✅ Ingestion configured for $INGESTION_MODE mode (Estimated duration: $ESTIMATED_DURATION)"
}

# Function to start data ingestion
start_ingestion() {
    if [ "$AUTO_START_INGESTION" = "false" ]; then
        print_status $YELLOW "⏭️  Skipping automatic ingestion (AUTO_START_INGESTION=false)"
        return 0
    fi
    
    print_step "7" "Starting data ingestion..."
    
    print_status $BLUE "Configuration:"
    echo "$INGESTION_CONFIG" | jq '.'
    echo ""
    
    # Start ingestion
    print_status $BLUE "Starting ingestion..."
    local response=$(curl -k -s -X POST "$BOT_URL/api/data-ingestion/start" \
        -H "Content-Type: application/json" \
        -d "$INGESTION_CONFIG")
    
    local ingestion_id=$(echo "$response" | jq -r '.ingestionId // empty')
    
    if [ -z "$ingestion_id" ] || [ "$ingestion_id" = "null" ]; then
        print_status $RED "❌ Failed to start ingestion:"
        echo "$response" | jq '.'
        return 1
    fi
    
    print_status $GREEN "✅ Ingestion started with ID: $ingestion_id"
    echo "$response" | jq '.'
    
    # Monitor progress
    monitor_ingestion "$ingestion_id"
}

# Function to monitor ingestion progress
monitor_ingestion() {
    local ingestion_id=$1
    
    print_step "8" "Monitoring ingestion progress..."
    
    local start_time=$(date +%s)
    local last_progress=0
    
    while true; do
        local progress_response=$(curl -k -s "$BOT_URL/api/data-ingestion/progress/$ingestion_id")
        local status=$(echo "$progress_response" | jq -r '.status // "unknown"')
        local progress_percentage=$(echo "$progress_response" | jq -r '.progressPercentage // 0')
        local processed_repos=$(echo "$progress_response" | jq -r '.processedRepositories // 0')
        local total_repos=$(echo "$progress_response" | jq -r '.totalRepositories // 0')
        local processed_prs=$(echo "$progress_response" | jq -r '.processedPRs // 0')
        local total_issues=$(echo "$progress_response" | jq -r '.totalIssues // 0')
        
        # Calculate elapsed time
        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))
        local elapsed_min=$((elapsed / 60))
        local elapsed_sec=$((elapsed % 60))
        
        # Calculate ETA
        local eta="Unknown"
        if [ "$progress_percentage" != "0" ] && [ "$progress_percentage" != "null" ]; then
            local remaining_percentage=$((100 - progress_percentage))
            if [ "$remaining_percentage" -gt 0 ]; then
                local eta_seconds=$((elapsed * remaining_percentage / progress_percentage))
                local eta_min=$((eta_seconds / 60))
                local eta_sec=$((eta_seconds % 60))
                eta="${eta_min}m ${eta_sec}s"
            fi
        fi
        
        # Print progress
        printf "\r${BLUE}Progress: %s%% | Repos: %d/%d | PRs: %d | Issues: %d | Elapsed: %dm %ds | ETA: %s${NC}" \
            "$progress_percentage" "$processed_repos" "$total_repos" "$processed_prs" "$total_issues" \
            "$elapsed_min" "$elapsed_sec" "$eta"
        
        # Check if completed
        case $status in
            "Completed")
                echo ""
                print_status $GREEN "✅ Ingestion completed successfully!"
                break
                ;;
            "Failed")
                echo ""
                print_status $RED "❌ Ingestion failed!"
                local error_msg=$(echo "$progress_response" | jq -r '.errorMessage // "Unknown error"')
                print_status $RED "Error: $error_msg"
                return 1
                ;;
            "Running"|"Queued")
                # Continue monitoring
                ;;
            *)
                echo ""
                print_status $YELLOW "⚠️  Unknown status: $status"
                ;;
        esac
        
        # Update progress if changed significantly
        if [ "$progress_percentage" != "$last_progress" ]; then
            last_progress=$progress_percentage
        fi
        
        sleep 10
    done
    
    # Show final results
    show_ingestion_results "$ingestion_id"
}

# Function to show ingestion results
show_ingestion_results() {
    local ingestion_id=$1
    
    print_step "9" "Ingestion Results"
    
    local final_response=$(curl -k -s "$BOT_URL/api/data-ingestion/progress/$ingestion_id")
    
    echo ""
    print_status $GREEN "📊 Final Results:"
    echo "$final_response" | jq '{
        status: .status,
        processedRepositories: .processedRepositories,
        totalRepositories: .totalRepositories,
        processedPRs: .processedPRs,
        totalIssues: .totalIssues,
        totalComments: .totalComments,
        duration: .duration
    }'
    
    # Get learning insights
    print_status $BLUE "Getting learning insights..."
    local insights=$(curl -k -s "$BOT_URL/api/feedback/insights")
    
    echo ""
    print_status $GREEN "🧠 Learning Insights:"
    echo "$insights" | jq '{
        totalPullRequestsAnalyzed: .totalPullRequestsAnalyzed,
        totalIssuesFound: .totalIssuesFound,
        averageIssuesPerPR: .averageIssuesPerPR,
        averageRuleEffectiveness: .averageRuleEffectiveness,
        developerSatisfactionScore: .developerSatisfactionScore
    }'
    
    # Get performance metrics
    print_status $BLUE "Getting performance metrics..."
    local performance=$(curl -k -s "$BOT_URL/api/performance/report")
    
    echo ""
    print_status $GREEN "⚡ Performance Summary:"
    echo "$performance" | jq '.Summary'
}

# Function to show bot status
show_bot_status() {
    print_step "10" "Bot Status & Endpoints"
    
    echo ""
    print_status $GREEN "🤖 Intelligent Code Review Bot is running!"
    echo ""
    print_status $BLUE "📡 Available Endpoints:"
    echo "  • Webhook URL: $BOT_URL/api/webhook"
    echo "  • Health Check: $BOT_URL/api/performance/report"
    echo "  • Learning Insights: $BOT_URL/api/feedback/insights"
    echo "  • Performance Report: $BOT_URL/api/performance/report"
    echo "  • Rule Effectiveness: $BOT_URL/api/feedback/rule-effectiveness"
    echo "  • Data Ingestion: $BOT_URL/api/data-ingestion/status"
    echo "  • Swagger UI: $BOT_URL/swagger"
    echo ""
    print_status $BLUE "📊 Monitoring Commands:"
    echo "  • Check health: curl -k $BOT_URL/api/performance/report"
    echo "  • View insights: curl -k $BOT_URL/api/feedback/insights"
    echo "  • Monitor performance: curl -k $BOT_URL/api/performance/report"
    echo ""
    print_status $BLUE "📝 Logs:"
    echo "  • Bot logs: tail -f logs/bot.log"
    echo "  • All logs: tail -f logs/codereview-bot-*.log"
    echo ""
    print_status $GREEN "🎉 Setup complete! Your intelligent bot is ready to learn and improve!"
}

# Function to cleanup on exit
cleanup() {
    print_status $YELLOW "Cleaning up..."
    
    # Kill bot process if running
    if [ -f "bot.pid" ]; then
        local bot_pid=$(cat bot.pid)
        if kill -0 "$bot_pid" 2>/dev/null; then
            print_status $BLUE "Stopping bot (PID: $bot_pid)..."
            kill "$bot_pid" || true
        fi
        rm -f bot.pid
    fi
}

# Set up signal handlers
trap cleanup EXIT INT TERM

# Main execution
main() {
    print_header
    
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --mode)
                INGESTION_MODE="$2"
                shift 2
                ;;
            --no-ingestion)
                AUTO_START_INGESTION="false"
                shift
                ;;
            --help)
                echo "Usage: $0 [OPTIONS]"
                echo ""
                echo "Options:"
                echo "  --mode MODE        Ingestion mode: small, medium, large, massive (default: large)"
                echo "  --no-ingestion     Skip automatic data ingestion"
                echo "  --help             Show this help message"
                echo ""
                echo "Environment Variables:"
                echo "  GITHUB_TOKEN       GitHub personal access token (required)"
                echo "  INGESTION_MODE     Ingestion mode (default: large)"
                echo "  AUTO_START_INGESTION  Auto-start ingestion (default: true)"
                echo ""
                echo "Examples:"
                echo "  $0                                    # Run with default settings"
                echo "  $0 --mode small --no-ingestion        # Start bot only, no ingestion"
                echo "  $0 --mode massive                     # Run massive ingestion"
                exit 0
                ;;
            *)
                print_status $RED "Unknown option: $1"
                echo "Use --help for usage information"
                exit 1
                ;;
        esac
    done
    
    # Run the setup process
    check_prerequisites
    build_project
    setup_directories
    start_bot
    wait_for_bot_ready
    configure_ingestion
    start_ingestion
    show_bot_status
    
    print_status $GREEN "🎉 All done! Your intelligent bot is running and learning!"
    echo ""
    print_status $YELLOW "Press Ctrl+C to stop the bot, or leave it running for continuous learning."
    
    # Keep script running to maintain bot
    while true; do
        sleep 30
        
        # Check if bot is still running
        if [ -f "bot.pid" ]; then
            local bot_pid=$(cat bot.pid)
            if ! kill -0 "$bot_pid" 2>/dev/null; then
                print_status $RED "❌ Bot process died unexpectedly!"
                print_status $BLUE "Restarting bot..."
                start_bot
                wait_for_bot_ready
            fi
        fi
    done
}

# Run main function
main "$@"
