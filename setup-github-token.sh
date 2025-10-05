#!/bin/bash

# GitHub Token Setup Script for Code Review Bot
# This script helps you set up your GitHub token for million PRs ingestion

echo "🔑 GitHub Token Setup for Million PRs Ingestion"
echo "==============================================="
echo ""

# Check if token is already set
if [ -n "$GITHUB_TOKEN" ]; then
    echo "✅ GitHub token is already set: ${GITHUB_TOKEN:0:8}..."
    echo ""
    echo "🔍 Testing token validity..."
    if curl -s -H "Authorization: token $GITHUB_TOKEN" https://api.github.com/user | grep -q '"login"'; then
        echo "✅ Token is valid and working!"
        echo ""
        echo "🚀 You're ready to run the ingestion:"
        echo "   ./ingest-millions-prs.sh"
    else
        echo "❌ Token appears to be invalid or expired"
        echo "Please create a new token at: https://github.com/settings/tokens"
    fi
    exit 0
fi

echo "📋 GitHub Token Setup Instructions:"
echo ""
echo "1. 🌐 Go to GitHub Token Settings:"
echo "   https://github.com/settings/tokens"
echo ""
echo "2. 🔑 Click 'Generate new token' → 'Generate new token (classic)'"
echo ""
echo "3. 📝 Configure your token:"
echo "   - Note: 'CodeReviewBot - Million PRs Ingestion'"
echo "   - Expiration: '90 days' (or your preference)"
echo ""
echo "4. ✅ Select these scopes:"
echo "   ☑️  repo (Full control of private repositories)"
echo "   ☑️  public_repo (Access public repositories)"
echo "   ☑️  read:org (Read org and team membership)"
echo ""
echo "5. 🎯 Click 'Generate token' and copy the token"
echo ""
echo "6. 💾 Choose your setup method:"
echo ""
echo "   Method 1 - Temporary (current session only):"
echo "   export GITHUB_TOKEN='ghp_your_actual_token_here'"
echo ""
echo "   Method 2 - Permanent (add to shell profile):"
echo "   echo 'export GITHUB_TOKEN=\"ghp_your_actual_token_here\"' >> ~/.zshrc"
echo "   source ~/.zshrc"
echo ""
echo "   Method 3 - Environment file (recommended):"
echo "   echo 'GITHUB_TOKEN=ghp_your_actual_token_here' > .env"
echo "   source .env"
echo ""
echo "7. 🧪 Test your token:"
echo "   ./setup-github-token.sh"
echo ""
echo "8. 🚀 Run the ingestion:"
echo "   ./ingest-millions-prs.sh"
echo ""
echo "⚠️  Important Security Notes:"
echo "   - Never commit your token to git"
echo "   - Use .env file and add it to .gitignore"
echo "   - Regenerate token if compromised"
echo "   - Set appropriate expiration date"
echo ""
echo "🔗 Useful Links:"
echo "   - Token Settings: https://github.com/settings/tokens"
echo "   - API Rate Limits: https://docs.github.com/en/rest/overview/resources-in-the-rest-api#rate-limiting"
echo "   - Token Scopes: https://docs.github.com/en/developers/apps/building-oauth-apps/scopes-for-oauth-apps"
