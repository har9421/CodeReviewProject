# Quick Start Script - Deploy Bot Without Publisher ID
# This script helps you deploy the bot as a web service and configure webhooks

param(
    [Parameter(Mandatory=$true)]
    [string]$OrganizationUrl,
    
    [Parameter(Mandatory=$true)]
    [string]$ProjectName,
    
    [Parameter(Mandatory=$true)]
    [string]$PersonalAccessToken,
    
    [Parameter(Mandatory=$false)]
    [string]$BotServiceUrl = "https://your-bot-service.azurewebsites.net"
)

Write-Host "🚀 Quick Start: Deploying Code Review Bot..." -ForegroundColor Green
Write-Host "This approach does NOT require a Publisher ID" -ForegroundColor Yellow
Write-Host ""

# Step 1: Build the bot service
Write-Host "📦 Step 1: Building bot service..." -ForegroundColor Cyan
Push-Location "src/CodeReviewBot"
try {
    dotnet publish -c Release -o ../../dist
    Write-Host "✅ Bot service built successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to build bot service: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Pop-Location

# Step 2: Configure webhook
Write-Host "`n🔗 Step 2: Configuring webhook..." -ForegroundColor Cyan
$webhookUrl = "$BotServiceUrl/api/webhook"

$headers = @{
    "Authorization" = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$PersonalAccessToken"))
    "Content-Type" = "application/json"
}

$webhookPayload = @{
    publisherId = "tfs"
    eventType = "git.pullrequest.created"
    resourceVersion = "1.0"
    consumerId = "webHooks"
    consumerActionId = "httpRequest"
    publisherInputs = @{
        projectId = "$ProjectName"
    }
    consumerInputs = @{
        url = "$webhookUrl"
        httpHeaders = @{
            "Content-Type" = "application/json"
        }
    }
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$OrganizationUrl/_apis/hooks/subscriptions?api-version=6.0" -Method POST -Headers $headers -Body $webhookPayload
    Write-Host "✅ Webhook created successfully" -ForegroundColor Green
    
    # Also create webhook for pull request updates
    $webhookPayload.eventType = "git.pullrequest.updated"
    $response2 = Invoke-RestMethod -Uri "$OrganizationUrl/_apis/hooks/subscriptions?api-version=6.0" -Method POST -Headers $headers -Body $webhookPayload
    Write-Host "✅ Update webhook created successfully" -ForegroundColor Green
    
} catch {
    Write-Host "❌ Failed to create webhook: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please check your Personal Access Token and organization URL" -ForegroundColor Yellow
}

# Step 3: Instructions
Write-Host "`n📋 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Deploy the 'dist' folder to your web hosting platform" -ForegroundColor White
Write-Host "2. Update the BotServiceUrl parameter with your actual service URL" -ForegroundColor White
Write-Host "3. Configure environment variables on your hosting platform:" -ForegroundColor White
Write-Host "   - SYSTEM_ACCESSTOKEN: $PersonalAccessToken" -ForegroundColor Gray
Write-Host "   - AI_API_KEY: (optional) Your OpenAI API key" -ForegroundColor Gray
Write-Host "4. Test by creating a pull request in your project" -ForegroundColor White

Write-Host "`n🎉 Quick setup completed!" -ForegroundColor Green
Write-Host "Bot Service URL: $BotServiceUrl" -ForegroundColor Cyan
Write-Host "Organization: $OrganizationUrl" -ForegroundColor Cyan
Write-Host "Project: $ProjectName" -ForegroundColor Cyan
Write-Host ""
Write-Host "💡 Tip: Once you're happy with the bot, you can later create a Publisher ID" -ForegroundColor Yellow
Write-Host "   and install it as an extension using install-bot.ps1" -ForegroundColor Yellow
