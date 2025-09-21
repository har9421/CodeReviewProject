# Code Review Bot - Local Development Setup
# This script helps you run the bot locally with ngrok for Azure DevOps integration

Write-Host "🚀 Starting Code Review Bot locally..." -ForegroundColor Green

# Check if ngrok is installed
try {
    $ngrokVersion = ngrok version
    Write-Host "✅ ngrok found: $ngrokVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ ngrok is not installed. Please install it first:" -ForegroundColor Red
    Write-Host "   - Download from: https://ngrok.com/download" -ForegroundColor Yellow
    Write-Host "   - Or install via package manager:" -ForegroundColor Yellow
    Write-Host "     Windows: choco install ngrok" -ForegroundColor Yellow
    Write-Host "     macOS: brew install ngrok" -ForegroundColor Yellow
    exit 1
}

# Check if .NET is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET found: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET is not installed. Please install .NET 8 SDK first:" -ForegroundColor Red
    Write-Host "   - Download from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Build the project
Write-Host "`n📦 Building the bot service..." -ForegroundColor Cyan
Push-Location "src/CodeReviewBot"
try {
    dotnet build -c Release
    Write-Host "✅ Build successful" -ForegroundColor Green
} catch {
    Write-Host "❌ Build failed. Please check the errors above." -ForegroundColor Red
    Pop-Location
    exit 1
}

# Start the bot service
Write-Host "`n🌐 Starting bot service on http://localhost:5000..." -ForegroundColor Cyan
$botJob = Start-Job -ScriptBlock {
    Set-Location "src/CodeReviewBot"
    dotnet run --urls="http://localhost:5000"
}

# Wait for the service to start
Write-Host "⏳ Waiting for service to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Test if the service is running
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/webhook/health" -TimeoutSec 5
    Write-Host "✅ Bot service is running successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Bot service failed to start" -ForegroundColor Red
    Stop-Job $botJob
    Remove-Job $botJob
    Pop-Location
    exit 1
}

# Start ngrok
Write-Host "`n🔗 Starting ngrok tunnel..." -ForegroundColor Cyan
$ngrokJob = Start-Job -ScriptBlock {
    ngrok http 5000
}

# Wait for ngrok to start
Start-Sleep -Seconds 3

# Get the public URL
try {
    $tunnels = Invoke-RestMethod -Uri "http://localhost:4040/api/tunnels" -TimeoutSec 5
    $publicUrl = $tunnels.tunnels | Where-Object { $_.proto -eq "https" } | Select-Object -First 1 | ForEach-Object { $_.public_url }
    
    if (-not $publicUrl) {
        throw "No public URL found"
    }
    
    Write-Host ""
    Write-Host "🎉 Code Review Bot is now running!" -ForegroundColor Green
    Write-Host "==================================" -ForegroundColor Cyan
    Write-Host "Local URL: http://localhost:5000" -ForegroundColor White
    Write-Host "Public URL: $publicUrl" -ForegroundColor White
    Write-Host "Webhook Endpoint: $publicUrl/api/webhook" -ForegroundColor White
    Write-Host "Health Check: $publicUrl/api/webhook/health" -ForegroundColor White
    Write-Host ""
    Write-Host "📋 Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Configure webhook in Azure DevOps:" -ForegroundColor White
    Write-Host "   .\configure-webhook.ps1 -OrganizationUrl 'https://dev.azure.com/yourorg' -ProjectName 'YourProject' -PersonalAccessToken 'your-pat' -BotServiceUrl '$publicUrl'" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Test the webhook:" -ForegroundColor White
    Write-Host "   curl $publicUrl/api/webhook/health" -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Create a pull request in your Azure DevOps project to test" -ForegroundColor White
    Write-Host ""
    Write-Host "Press Ctrl+C to stop the bot and ngrok" -ForegroundColor Yellow
    
} catch {
    Write-Host "❌ Failed to get ngrok URL: $($_.Exception.Message)" -ForegroundColor Red
    Stop-Job $botJob, $ngrokJob
    Remove-Job $botJob, $ngrokJob
    Pop-Location
    exit 1
}

Pop-Location

# Keep the script running and handle cleanup
try {
    Write-Host "`nPress Ctrl+C to stop..." -ForegroundColor Yellow
    while ($true) {
        Start-Sleep -Seconds 1
    }
} finally {
    Write-Host "`n🛑 Stopping services..." -ForegroundColor Yellow
    Stop-Job $botJob, $ngrokJob -ErrorAction SilentlyContinue
    Remove-Job $botJob, $ngrokJob -ErrorAction SilentlyContinue
    Write-Host "✅ Services stopped" -ForegroundColor Green
}
