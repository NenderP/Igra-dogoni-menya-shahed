@echo off
rem Gacha simulation: 100000 pulls, rates and pity report.
cd /d "%~dp0"
dotnet run --project server -- sim
pause
