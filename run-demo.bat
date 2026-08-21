@echo off
rem Demo battle: easy bot vs hard bot, log printed to console.
cd /d "%~dp0"
dotnet run --project server -- demo
pause
