#!/bin/bash

# Direct Azure DevOps API test
echo "🔍 Direct Azure DevOps API Test"
echo "================================"

# Set your PAT token here
export SYSTEM_ACCESSTOKEN="${SYSTEM_ACCESSTOKEN:-}"

if [ -z "$SYSTEM_ACCESSTOKEN" ]; then
    echo "❌ SYSTEM_ACCESSTOKEN is not set"
    echo "Please set your Azure DevOps PAT token:"
    echo "export SYSTEM_ACCESSTOKEN='your-pat-token'"
    exit 1
fi

echo "✅ PAT token is set"
echo ""

# Test 1: Get repository details
echo "📋 Test 1: Get repository details"
echo "--------------------------------"
REPO_URL="https://dev.azure.com/khUniverse/sso/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8?api-version=7.0"
echo "URL: $REPO_URL"
curl -s -u ":$SYSTEM_ACCESSTOKEN" "$REPO_URL" | jq '.name, .id' 2>/dev/null || echo "Failed to parse JSON"
echo ""

# Test 2: List pull requests
echo "📋 Test 2: List pull requests"
echo "----------------------------"
PR_LIST_URL="https://dev.azure.com/khUniverse/sso/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8/pullRequests?api-version=7.0"
echo "URL: $PR_LIST_URL"
curl -s -u ":$SYSTEM_ACCESSTOKEN" "$PR_LIST_URL" | jq '.value[] | {pullRequestId, title, status}' 2>/dev/null || echo "Failed to parse JSON"
echo ""

# Test 3: Get pull request changes (this is the failing one)
echo "📋 Test 3: Get pull request changes"
echo "----------------------------------"
PR_CHANGES_URL="https://dev.azure.com/khUniverse/sso/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8/pullRequests/128/changes?api-version=7.0"
echo "URL: $PR_CHANGES_URL"
echo "Response:"
curl -s -u ":$SYSTEM_ACCESSTOKEN" "$PR_CHANGES_URL" | head -c 200
echo ""
echo ""

# Test 4: Try different API versions
echo "📋 Test 4: Try different API versions"
echo "------------------------------------"
for version in "7.0" "6.0" "5.1" "4.1"; do
    echo "Testing API version $version:"
    TEST_URL="https://dev.azure.com/khUniverse/sso/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8/pullRequests/128/changes?api-version=$version"
    STATUS=$(curl -s -o /dev/null -w "%{http_code}" -u ":$SYSTEM_ACCESSTOKEN" "$TEST_URL")
    echo "  Status: $STATUS"
    if [ "$STATUS" = "200" ]; then
        echo "  ✅ Success with version $version!"
        curl -s -u ":$SYSTEM_ACCESSTOKEN" "$TEST_URL" | jq '.changes | length' 2>/dev/null || echo "  Failed to parse JSON"
    else
        echo "  ❌ Failed with version $version"
    fi
    echo ""
done

echo "🏁 Test completed!"
