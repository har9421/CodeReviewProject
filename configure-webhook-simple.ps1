# Simple Webhook Configuration for Code Review Bot
# This script creates webhooks for Azure DevOps pull request events

param(
    [Parameter(Mandatory=$true)]
    [string]$OrganizationUrl,
    
    [Parameter(Mandatory=$true)]
    [string]$ProjectName,
    
    [Parameter(Mandatory=$true)]
    [string]$PersonalAccessToken,
    
    [Parameter(Mandatory=$true)]
    [string]$BotServiceUrl
)

Write-Host "🔗 Configuring webhook for Code Review Bot..." -ForegroundColor Green
Write-Host "Organization: $OrganizationUrl" -ForegroundColor Cyan
Write-Host "Project: $ProjectName" -ForegroundColor Cyan
Write-Host "Bot URL: $BotServiceUrl" -ForegroundColor Cyan

# Bot webhook URL
$webhookUrl = "$BotServiceUrl/api/webhook"

# Create authorization header
$base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$PersonalAccessToken"))

$headers = @{
    "Authorization" = "Basic $base64AuthInfo"
    "Content-Type" = "application/json"
}

# Function to create webhook subscription
function Create-WebhookSubscription {
    param(
        [string]$EventType,
        [string]$Description
    )
    
    Write-Host "Creating webhook for: $Description" -ForegroundColor Yellow
    
    $webhookPayload = @{
        publisherId = "tfs"
        eventType = $EventType
        resourceVersion = "1.0"
        consumerId = "webHooks"
        consumerActionId = "httpRequest"
        publisherInputs = @{
            projectId = $ProjectName
        }
        consumerInputs = @{
            url = $webhookUrl
            httpHeaders = @{
                "Content-Type" = "application/json"
            }
        }
    }
    
    $jsonPayload = $webhookPayload | ConvertTo-Json -Depth 10
    
    try {
        $response = Invoke-RestMethod -Uri "$OrganizationUrl/_apis/hooks/subscriptions?api-version=6.0" -Method POST -Headers $headers -Body $jsonPayload
        Write-Host "✅ $Description webhook created successfully with ID: $($response.id)" -ForegroundColor Green
        return $response
    } catch {
        Write-Host "❌ Failed to create $Description webhook: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "Response: $responseBody" -ForegroundColor Red
        }
        return $null
    }
}

# Create webhooks for pull request events
Write-Host ""
Write-Host "Creating webhook subscriptions..." -ForegroundColor Green

$webhook1 = Create-WebhookSubscription -EventType "git.pullrequest.created" -Description "Pull Request Created"
$webhook2 = Create-WebhookSubscription -EventType "git.pullrequest.updated" -Description "Pull Request Updated"

Write-Host ""
Write-Host "🎉 Webhook configuration completed!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Summary:" -ForegroundColor Cyan
Write-Host "  Bot Service URL: $BotServiceUrl" -ForegroundColor White
Write-Host "  Webhook Endpoint: $webhookUrl" -ForegroundColor White
Write-Host "  Organization: $OrganizationUrl" -ForegroundColor White
Write-Host "  Project: $ProjectName" -ForegroundColor White

if ($webhook1 -and $webhook2) {
    Write-Host ""
    Write-Host "✅ Both webhooks created successfully!" -ForegroundColor Green
    Write-Host "You can now create pull requests to test the integration." -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "⚠️  Some webhooks may not have been created. Check the errors above." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🔍 To test:" -ForegroundColor Cyan
Write-Host "  1. Create a pull request in your Azure DevOps project" -ForegroundColor White
Write-Host "  2. Check your bot logs for webhook events" -ForegroundColor White
Write-Host "  3. Verify webhook delivery in Azure DevOps Service Hooks" -ForegroundColor White
