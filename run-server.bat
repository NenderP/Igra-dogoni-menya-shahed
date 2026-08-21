@echo off
rem Запуск игрового сервера. Окно держи открытым, пока играешь.
rem Сервер слушает ws://localhost:5050/ws
cd /d "%~dp0"
echo ============================================
echo   Igra server: ws://localhost:5050/ws
echo   Для остановки закрой это окно или Ctrl+C
echo ============================================
dotnet run --project server
pause
