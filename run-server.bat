@echo off
chcp 65001 >nul
rem Game server launcher. Keep this window open while playing.
rem Server listens on ws://localhost:5050/ws
cd /d "%~dp0"

taskkill /IM Server.exe /F >nul 2>&1

dotnet build server\Server.csproj -v q --nologo
if errorlevel 1 (echo [ERROR] Server build failed, see messages above.& pause& exit /b 1)

echo ============================================
echo   Igra server: ws://localhost:5050/ws
echo   Close this window or press Ctrl+C to stop
echo ============================================
dotnet run --no-build --project server
pause
