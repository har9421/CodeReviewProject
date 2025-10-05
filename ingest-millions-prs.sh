#!/bin/bash

# Million C# PRs Data Ingestion Script
# This script ingests millions of C# pull requests for bot learning

echo "🚀 Starting Million C# PRs Data Ingestion"
echo "=========================================="

# Configuration
BOT_URL="http://localhost:5002"
GITHUB_TOKEN="${GITHUB_TOKEN:-}"
BATCH_SIZE=1000
MAX_REPOSITORIES=1000
MAX_PRS_PER_REPO=1000

# Check if GitHub token is set
if [ -z "$GITHUB_TOKEN" ]; then
    echo "❌ Error: GITHUB_TOKEN environment variable is not set"
    echo "Please set your GitHub token:"
    echo "export GITHUB_TOKEN='your-github-token-here'"
    exit 1
fi

# Check if bot is running
echo "🔍 Checking if bot is running..."
if ! curl -s "$BOT_URL/api/performance/health" > /dev/null; then
    echo "❌ Error: Bot is not running at $BOT_URL"
    echo "Please start the bot first:"
    echo "./start-intelligent-bot.sh"
    exit 1
fi

echo "✅ Bot is running"

# Function to make API calls
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

# Function to wait for ingestion to complete
wait_for_completion() {
    local ingestion_id=$1
    local max_wait_minutes=${2:-60}
    local wait_seconds=0
    local max_wait_seconds=$((max_wait_minutes * 60))
    
    echo "⏳ Waiting for ingestion to complete (max ${max_wait_minutes} minutes)..."
    
    while [ $wait_seconds -lt $max_wait_seconds ]; do
        local status=$(api_call "GET" "/api/data-ingestion/progress/$ingestion_id" | jq -r '.status')
        
        case $status in
            "Completed")
                echo "✅ Ingestion completed successfully!"
                return 0
                ;;
            "Failed")
                echo "❌ Ingestion failed!"
                return 1
                ;;
            "Running"|"Queued")
                echo "🔄 Status: $status - Elapsed: $((wait_seconds / 60))m $((wait_seconds % 60))s"
                sleep 30
                wait_seconds=$((wait_seconds + 30))
                ;;
            *)
                echo "❓ Unknown status: $status"
                sleep 30
                wait_seconds=$((wait_seconds + 30))
                ;;
        esac
    done
    
    echo "⏰ Timeout reached. Ingestion may still be running."
    return 2
}

# Function to get ingestion recommendations
get_recommendations() {
    echo "📋 Getting ingestion recommendations..."
    api_call "GET" "/api/data-ingestion/recommendations" | jq '.'
}

# Function to start large-scale ingestion
start_large_scale_ingestion() {
    echo "🚀 Starting large-scale C# PR ingestion..."
    
    # Configuration for large-scale ingestion
    local config='{
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
            "microsoft/azure-storage-tables-dotnet",
            "microsoft/azure-cosmos-dotnet-v2",
            "microsoft/azure-cosmos-dotnet-v4",
            "microsoft/azure-cosmos-dotnet-v5",
            "microsoft/azure-cosmos-dotnet-v6",
            "microsoft/azure-cosmos-dotnet-v7",
            "microsoft/azure-cosmos-dotnet-v8",
            "microsoft/azure-cosmos-dotnet-v9",
            "microsoft/azure-cosmos-dotnet-v10"
        ],
        "maxRepositories": 1000,
        "maxPRsPerRepository": 1000,
        "startDate": "2022-01-01T00:00:00Z",
        "endDate": "2024-01-01T00:00:00Z",
        "languages": ["csharp"]
    }'
    
    echo "📊 Configuration:"
    echo "$config" | jq '.'
    
    # Start ingestion
    local response=$(api_call "POST" "/api/data-ingestion/start" "$config")
    local ingestion_id=$(echo "$response" | jq -r '.ingestionId')
    
    if [ "$ingestion_id" = "null" ] || [ -z "$ingestion_id" ]; then
        echo "❌ Failed to start ingestion:"
        echo "$response" | jq '.'
        exit 1
    fi
    
    echo "✅ Ingestion started with ID: $ingestion_id"
    echo "$response" | jq '.'
    
    # Wait for completion
    wait_for_completion "$ingestion_id" 120  # 2 hours max
    
    # Get final results
    echo "📊 Final results:"
    api_call "GET" "/api/data-ingestion/progress/$ingestion_id" | jq '.'
}

# Function to start batch ingestion
start_batch_ingestion() {
    local batch_number=$1
    local start_date=$2
    local end_date=$3
    
    echo "🔄 Starting batch $batch_number ($start_date to $end_date)..."
    
    local config="{
        \"usePopularRepositories\": true,
        \"maxRepositories\": 100,
        \"maxPRsPerRepository\": 500,
        \"startDate\": \"$start_date\",
        \"endDate\": \"$end_date\",
        \"languages\": [\"csharp\"]
    }"
    
    local response=$(api_call "POST" "/api/data-ingestion/start" "$config")
    local ingestion_id=$(echo "$response" | jq -r '.ingestionId')
    
    if [ "$ingestion_id" = "null" ] || [ -z "$ingestion_id" ]; then
        echo "❌ Failed to start batch $batch_number"
        return 1
    fi
    
    echo "✅ Batch $batch_number started: $ingestion_id"
    return 0
}

# Function to run multiple batches
run_multiple_batches() {
    echo "🔄 Running multiple batches for comprehensive data ingestion..."
    
    # Define batch periods (6 months each)
    local batches=(
        "2020-01-01T00:00:00Z 2020-06-30T23:59:59Z"
        "2020-07-01T00:00:00Z 2020-12-31T23:59:59Z"
        "2021-01-01T00:00:00Z 2021-06-30T23:59:59Z"
        "2021-07-01T00:00:00Z 2021-12-31T23:59:59Z"
        "2022-01-01T00:00:00Z 2022-06-30T23:59:59Z"
        "2022-07-01T00:00:00Z 2022-12-31T23:59:59Z"
        "2023-01-01T00:00:00Z 2023-06-30T23:59:59Z"
        "2023-07-01T00:00:00Z 2023-12-31T23:59:59Z"
    )
    
    local batch_number=1
    for batch in "${batches[@]}"; do
        local start_date=$(echo $batch | cut -d' ' -f1)
        local end_date=$(echo $batch | cut -d' ' -f2)
        
        echo "📅 Processing batch $batch_number: $start_date to $end_date"
        
        if start_batch_ingestion $batch_number "$start_date" "$end_date"; then
            echo "✅ Batch $batch_number completed successfully"
        else
            echo "❌ Batch $batch_number failed"
        fi
        
        batch_number=$((batch_number + 1))
        
        # Wait between batches to avoid rate limiting
        echo "⏳ Waiting 5 minutes before next batch..."
        sleep 300
    done
}

# Function to monitor all ingestions
monitor_ingestions() {
    echo "📊 Current ingestion status:"
    api_call "GET" "/api/data-ingestion/status" | jq '.'
}

# Function to get learning insights
get_learning_insights() {
    echo "🧠 Learning insights:"
    api_call "GET" "/api/feedback/insights" | jq '.'
}

# Function to get performance metrics
get_performance_metrics() {
    echo "⚡ Performance metrics:"
    api_call "GET" "/api/performance/report" | jq '.Summary'
}

# Main menu
show_menu() {
    echo ""
    echo "🎯 Million C# PRs Data Ingestion Menu"
    echo "====================================="
    echo "1. Get recommendations"
    echo "2. Start large-scale ingestion (1000 repos, 1M+ PRs)"
    echo "3. Run multiple batches (comprehensive coverage)"
    echo "4. Monitor current ingestions"
    echo "5. Get learning insights"
    echo "6. Get performance metrics"
    echo "7. Exit"
    echo ""
    read -p "Select option (1-7): " choice
}

# Main execution
main() {
    echo "🎯 Million C# PRs Data Ingestion System"
    echo "======================================="
    echo "This script will help you ingest millions of C# pull requests"
    echo "to train your intelligent code review bot."
    echo ""
    
    while true; do
        show_menu
        
        case $choice in
            1)
                get_recommendations
                ;;
            2)
                echo "⚠️  Warning: This will start a large-scale ingestion that may take hours."
                read -p "Are you sure? (y/N): " confirm
                if [[ $confirm =~ ^[Yy]$ ]]; then
                    start_large_scale_ingestion
                fi
                ;;
            3)
                echo "⚠️  Warning: This will run multiple batches over several hours."
                read -p "Are you sure? (y/N): " confirm
                if [[ $confirm =~ ^[Yy]$ ]]; then
                    run_multiple_batches
                fi
                ;;
            4)
                monitor_ingestions
                ;;
            5)
                get_learning_insights
                ;;
            6)
                get_performance_metrics
                ;;
            7)
                echo "👋 Goodbye!"
                exit 0
                ;;
            *)
                echo "❌ Invalid option. Please select 1-7."
                ;;
        esac
        
        echo ""
        read -p "Press Enter to continue..."
    done
}

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo "❌ Error: jq is not installed"
    echo "Please install jq:"
    echo "  macOS: brew install jq"
    echo "  Ubuntu: sudo apt-get install jq"
    echo "  Windows: choco install jq"
    exit 1
fi

# Run main function
main
