#!/bin/bash

# Test Azure DevOps API directly with curl
# This will help us debug the 404 issue

echo "=== Testing Azure DevOps API ==="
echo

# Check if PAT is set
if [ -z "$SYSTEM_ACCESSTOKEN" ]; then
    echo "❌ SYSTEM_ACCESSTOKEN is not set"
    echo "Please set your Azure DevOps PAT token:"
    echo "export SYSTEM_ACCESSTOKEN='your-pat-token-here'"
    exit 1
fi

echo "✅ PAT token is set"
echo

# Set variables
ORG="https://dev.azure.com/khUniverse"
PROJECT="sso"
REPO_ID="801d272d-36b5-4f23-9674-01aa63f48ce8"
PR_ID="128"

# Create base64 encoded auth
AUTH=$(echo -n ":$SYSTEM_ACCESSTOKEN" | base64)

echo "=== Test 1: Repository Info ==="
echo "URL: $ORG/$PROJECT/_apis/git/repositories/$REPO_ID?api-version=7.0"
curl -s -H "Authorization: Basic $AUTH" \
     -H "Content-Type: application/json" \
     "$ORG/$PROJECT/_apis/git/repositories/$REPO_ID?api-version=7.0" | jq '.name, .id' 2>/dev/null || echo "Failed to get repository info"
echo

echo "=== Test 2: List Pull Requests ==="
echo "URL: $ORG/$PROJECT/_apis/git/repositories/$REPO_ID/pullRequests?api-version=7.0"
curl -s -H "Authorization: Basic $AUTH" \
     -H "Content-Type: application/json" \
     "$ORG/$PROJECT/_apis/git/repositories/$REPO_ID/pullRequests?api-version=7.0" | jq '.value[] | {pullRequestId, title, status}' 2>/dev/null || echo "Failed to list pull requests"
echo

echo "=== Test 3: Try PR Changes (different API versions) ==="
for version in "7.0" "6.0" "5.1" "4.1"; do
    echo "Trying API version $version..."
    echo "URL: $ORG/$PROJECT/_apis/git/repositories/$REPO_ID/pullRequests/$PR_ID/changes?api-version=$version"
    response=$(curl -s -w "%{http_code}" -H "Authorization: Basic $AUTH" \
                    -H "Content-Type: application/json" \
                    "$ORG/$PROJECT/_apis/git/repositories/$REPO_ID/pullRequests/$PR_ID/changes?api-version=$version")
    
    http_code="${response: -3}"
    body="${response%???}"
    
    if [ "$http_code" = "200" ]; then
        echo "✅ SUCCESS with API version $version!"
        echo "$body" | jq '.value | length' 2>/dev/null && echo " changes found"
        break
    else
        echo "❌ Failed with API version $version (HTTP $http_code)"
        echo "$body" | jq '.message' 2>/dev/null || echo "$body"
    fi
    echo
done

echo
echo "=== Test 4: Try with repository name instead of ID ==="
echo "First, let's get the repository name..."
repo_name=$(curl -s -H "Authorization: Basic $AUTH" \
                  -H "Content-Type: application/json" \
                  "$ORG/$PROJECT/_apis/git/repositories/$REPO_ID?api-version=7.0" | jq -r '.name' 2>/dev/null)

if [ "$repo_name" != "null" ] && [ -n "$repo_name" ]; then
    echo "Repository name: $repo_name"
    echo "Trying with repository name..."
    echo "URL: $ORG/$PROJECT/_apis/git/repositories/$repo_name/pullRequests/$PR_ID/changes?api-version=7.0"
    response=$(curl -s -w "%{http_code}" -H "Authorization: Basic $AUTH" \
                    -H "Content-Type: application/json" \
                    "$ORG/$PROJECT/_apis/git/repositories/$repo_name/pullRequests/$PR_ID/changes?api-version=7.0")
    
    http_code="${response: -3}"
    body="${response%???}"
    
    if [ "$http_code" = "200" ]; then
        echo "✅ SUCCESS with repository name!"
        echo "$body" | jq '.value | length' 2>/dev/null && echo " changes found"
    else
        echo "❌ Failed with repository name (HTTP $http_code)"
        echo "$body" | jq '.message' 2>/dev/null || echo "$body"
    fi
else
    echo "❌ Could not get repository name"
fi

echo
echo "=== Test Complete ==="
