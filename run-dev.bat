@echo off
chcp 65001 >nul
rem One-click dev launch: builds server, starts it in background, opens game client.
cd /d "%~dp0"

taskkill /IM Server.exe /F >nul 2>&1

dotnet build server\Server.csproj -v q --nologo
if errorlevel 1 (echo [ERROR] Server build failed, see messages above.& pause& exit /b 1)

start "" dotnet run --no-build --project server
dotnet run --project client
pause
