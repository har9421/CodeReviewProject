# Start Code Review Bot with ngrok support
# This script sets up the environment to work properly with ngrok

Write-Host "🚀 Starting Code Review Bot with ngrok support..." -ForegroundColor Green

# Set environment variable to indicate we're behind a proxy
$env:NGROK_URL = "https://ngrok.io"

# Navigate to the presentation project
Set-Location src\CodeReviewBot.Presentation

# Start the bot
Write-Host "Starting bot on http://localhost:5000..." -ForegroundColor Yellow
dotnet run

Write-Host "✅ Bot started successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "💡 Tips:" -ForegroundColor Cyan
Write-Host "- The bot is now running on http://localhost:5000" -ForegroundColor White
Write-Host "- HTTPS redirection is disabled for ngrok compatibility" -ForegroundColor White
Write-Host "- Start ngrok in another terminal: ngrok http 5000" -ForegroundColor White
Write-Host "- Use the ngrok HTTPS URL for your webhook configuration" -ForegroundColor White
