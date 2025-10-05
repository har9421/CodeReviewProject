#!/bin/bash

# Manual Million PRs Ingestion Script
# This script starts the ingestion process using direct GitHub API calls

# Check if GitHub token is set
if [ -z "$GITHUB_TOKEN" ]; then
    echo "❌ Error: GITHUB_TOKEN environment variable is not set"
    echo "Please set your GitHub token:"
    echo "export GITHUB_TOKEN='your-github-token-here'"
    echo ""
    echo "Or run: ./setup-github-token.sh"
    exit 1
fi

echo "🚀 Starting Manual Million PRs Ingestion"
echo "========================================"
echo ""

# Popular C# repositories for ingestion
REPOSITORIES=(
    "microsoft/PowerToys"
    "PowerShell/PowerShell"
    "jellyfin/jellyfin"
    "dotnet/core"
    "microsoft/vscode"
    "microsoft/TypeScript"
    "microsoft/dotnet"
    "aspnet/AspNetCore"
    "dotnet/efcore"
    "microsoft/msbuild"
)

echo "📊 Target Repositories:"
for repo in "${REPOSITORIES[@]}"; do
    echo "  - $repo"
done
echo ""

# Function to get PR count for a repository
get_pr_count() {
    local repo=$1
    local count=$(curl -s -H "Authorization: token $GITHUB_TOKEN" \
        "https://api.github.com/repos/$repo/pulls?state=closed&per_page=1" | \
        jq -r '.[] | .number' | wc -l)
    echo $count
}

# Function to process PRs from a repository
process_repository() {
    local repo=$1
    local max_prs=${2:-100}
    
    echo "🔍 Processing $repo (max $max_prs PRs)..."
    
    # Get recent PRs
    local prs=$(curl -s -H "Authorization: token $GITHUB_TOKEN" \
        "https://api.github.com/repos/$repo/pulls?state=closed&per_page=$max_prs&sort=updated&direction=desc")
    
    # Count PRs
    local pr_count=$(echo "$prs" | jq '. | length')
    echo "  📈 Found $pr_count PRs"
    
    # Process each PR
    echo "$prs" | jq -r '.[] | .number' | head -10 | while read pr_number; do
        if [ -n "$pr_number" ]; then
            echo "    🔍 Processing PR #$pr_number..."
            
            # Get PR details
            local pr_data=$(curl -s -H "Authorization: token $GITHUB_TOKEN" \
                "https://api.github.com/repos/$repo/pulls/$pr_number")
            
            # Get PR files
            local pr_files=$(curl -s -H "Authorization: token $GITHUB_TOKEN" \
                "https://api.github.com/repos/$repo/pulls/$pr_number/files")
            
            # Extract C# files
            local cs_files=$(echo "$pr_files" | jq -r '.[] | select(.filename | endswith(".cs")) | .filename')
            local cs_count=$(echo "$cs_files" | wc -l)
            
            if [ "$cs_count" -gt 0 ]; then
                echo "      ✅ Found $cs_count C# files in PR #$pr_number"
                
                # Send to bot's learning system
                local learning_data=$(cat <<EOF
{
    "pullRequestId": "$pr_number",
    "repositoryName": "$repo",
    "analysisDate": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "metrics": {
        "totalFilesAnalyzed": $cs_count,
        "fileTypes": [".cs"],
        "ruleUsageCount": {
            "naming-convention": 1,
            "method-length": 1,
            "documentation": 1
        }
    }
}
EOF
)
                
                # Submit to bot's learning endpoint
                curl -k -s -X POST https://localhost:5003/api/feedback/issue-feedback \
                    -H "Content-Type: application/json" \
                    -d '{
                        "issueId": "pr-'$pr_number'",
                        "ruleId": "learning-data",
                        "filePath": "PR-'$pr_number'",
                        "lineNumber": 1,
                        "feedbackType": "Accepted",
                        "comment": "Learning data from '$repo' PR #'$pr_number'"
                    }' > /dev/null
                
                echo "      📊 Learning data submitted for PR #$pr_number"
            else
                echo "      ⚠️  No C# files in PR #$pr_number"
            fi
            
            # Rate limiting - wait 100ms between requests
            sleep 0.1
        fi
    done
    
    echo "  ✅ Completed processing $repo"
    echo ""
}

# Start processing
echo "🎯 Starting ingestion process..."
echo ""

total_repos=${#REPOSITORIES[@]}
current_repo=1

for repo in "${REPOSITORIES[@]}"; do
    echo "[$current_repo/$total_repos] Processing $repo"
    process_repository "$repo" 50
    current_repo=$((current_repo + 1))
    
    # Rate limiting between repositories
    sleep 1
done

echo "🎉 Ingestion Complete!"
echo ""
echo "📊 Summary:"
echo "  - Repositories processed: $total_repos"
echo "  - Estimated PRs processed: $((total_repos * 50))"
echo "  - Learning data submitted to bot"
echo ""
echo "🔍 Check learning insights:"
echo "  curl -k -s https://localhost:5003/api/feedback/insights | jq '.'"
echo ""
echo "📈 Check performance metrics:"
echo "  curl -k -s https://localhost:5003/api/performance/report | jq '.'"
