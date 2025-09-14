#!/bin/bash

echo "🚀 CodeReviewRunner Enterprise Test"
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
