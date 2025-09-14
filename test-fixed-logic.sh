#!/bin/bash

echo "🔧 Testing Fixed Azure DevOps Logic"
echo "=================================="
echo ""

# Check if PAT token is set
if [ -z "$SYSTEM_ACCESSTOKEN" ]; then
    echo "⚠️  SYSTEM_ACCESSTOKEN is not set"
    echo "   This means we'll run in TEST MODE (no Azure DevOps API calls)"
    echo ""
    echo "   To test with real Azure DevOps API:"
    echo "   export SYSTEM_ACCESSTOKEN='your-pat-token'"
    echo ""
    echo "   Running in TEST MODE now..."
    echo ""
    
    # Run in test mode
    dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj \
        test \
        "https://raw.githubusercontent.com/microsoft/vscode/main/package.json" \
        test \
        "https://dev.azure.com/khUniverse" \
        "sso" \
        "801d272d-36b5-4f23-9674-01aa63f48ce8"
else
    echo "✅ SYSTEM_ACCESSTOKEN is set - running with Azure DevOps API"
    echo ""
    echo "🔍 Testing with the working pull request endpoint:"
    echo "   https://dev.azure.com/khUniverse/sso/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8/pullRequests/128?api-version=7.0"
    echo ""
    
    # Run with real Azure DevOps API
    dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj \
        "/tmp/test-repo" \
        "https://raw.githubusercontent.com/microsoft/vscode/main/package.json" \
        "128" \
        "https://dev.azure.com/khUniverse" \
        "sso" \
        "801d272d-36b5-4f23-9674-01aa63f48ce8"
fi

echo ""
echo "🏁 Test completed!"
echo ""
echo "📋 What the new logic does:"
echo "   1. ✅ First fetches pull request details (using your working URL)"
echo "   2. ✅ Shows PR status, source/target branches"
echo "   3. ✅ Tries multiple approaches to get changes:"
echo "      - Direct changes endpoint"
echo "      - Commits endpoint (as fallback)"
echo "      - Multiple API versions (7.0, 6.0, 5.1, 4.1)"
echo "   4. ✅ Provides detailed logging for each attempt"
echo ""
echo "🎯 Expected results:"
echo "   - Should successfully fetch PR details"
echo "   - Will show which approach works for getting changes"
echo "   - Detailed logs will help identify the correct API pattern"
