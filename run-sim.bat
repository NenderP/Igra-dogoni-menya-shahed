@echo off
rem Симуляция гачи: 100 000 круток, отчёт по шансам и пити
cd /d "%~dp0"
dotnet run --project server -- sim
pause
