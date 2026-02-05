# SQL Migration Tool - PowerShell Publish Script

Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "SQL Migration Tool - Publish Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Choose publish option:" -ForegroundColor Yellow
Write-Host "1. Self-contained (recommended for Server 2016)"
Write-Host "2. Framework-dependent (requires .NET 8.0 runtime)"
Write-Host ""

$choice = Read-Host "Enter choice (1 or 2)"

if ($choice -eq "1") {
    Write-Host ""
    Write-Host "Publishing self-contained version..." -ForegroundColor Green
    dotnet publish -c Release -r win-x64 /p:Platform=x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false
    Write-Host ""
    Write-Host "✓ Published to: bin\Release\net8.0-windows\win-x64\publish\" -ForegroundColor Green
    Write-Host ""
    Write-Host "This version includes all dependencies and works on Server 2016+" -ForegroundColor Cyan
}
elseif ($choice -eq "2") {
    Write-Host ""
    Write-Host "Publishing framework-dependent version..." -ForegroundColor Green
    dotnet publish -c Release -r win-x64 /p:Platform=x64 --self-contained false
    Write-Host ""
    Write-Host "✓ Published to: bin\Release\net8.0-windows\win-x64\publish\" -ForegroundColor Green
    Write-Host ""
    Write-Host "Note: This version requires .NET 8.0 Desktop Runtime" -ForegroundColor Yellow
}
else {
    Write-Host "Invalid choice!" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Publish complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Read-Host "Press Enter to exit"
