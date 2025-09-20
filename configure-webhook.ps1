# Webhook Configuration for Code Review Bot
# Run this script after installing the extension

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

Write-Host "🔗 Configuring webhook for Code Review Bot..." -ForegroundColor Green

# Bot webhook URL
$webhookUrl = "$BotServiceUrl/api/webhook"

# Create webhook subscription
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
    Write-Host "✅ Webhook created successfully with ID: $($response.id)" -ForegroundColor Green
    
    # Also create webhook for pull request updates
    $webhookPayload.eventType = "git.pullrequest.updated"
    $response2 = Invoke-RestMethod -Uri "$OrganizationUrl/_apis/hooks/subscriptions?api-version=6.0" -Method POST -Headers $headers -Body $webhookPayload
    Write-Host "✅ Update webhook created successfully with ID: $($response2.id)" -ForegroundColor Green
    
} catch {
    Write-Host "❌ Failed to create webhook: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please check your Personal Access Token and organization URL" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🎉 Webhook configuration completed!" -ForegroundColor Green
Write-Host "Bot Service URL: $BotServiceUrl" -ForegroundColor Cyan
Write-Host "Organization: $OrganizationUrl" -ForegroundColor Cyan
Write-Host "Project: $ProjectName" -ForegroundColor Cyan
