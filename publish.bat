@echo off
echo ========================================
echo SQL Migration Tool - Publish Script
echo ========================================
echo.
echo Choose publish option:
echo 1. Self-contained (recommended for Server 2016)
echo 2. Framework-dependent (requires .NET 8.0 runtime)
echo.
set /p choice="Enter choice (1 or 2): "

if "%choice%"=="1" (
    echo.
    echo Publishing self-contained version...
    dotnet publish -c Release -r win-x64 /p:Platform=x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false
    echo.
    echo ✓ Published to: bin\Release\net8.0-windows\win-x64\publish\
    echo.
    echo This version includes all dependencies and works on Server 2016+
)

if "%choice%"=="2" (
    echo.
    echo Publishing framework-dependent version...
    dotnet publish -c Release -r win-x64 /p:Platform=x64 --self-contained false
    echo.
    echo ✓ Published to: bin\Release\net8.0-windows\win-x64\publish\
    echo.
    echo Note: This version requires .NET 8.0 Desktop Runtime to be installed
)

echo.
echo ========================================
echo Publish complete!
echo ========================================
pause
