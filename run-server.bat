@echo off
chcp 65001 >nul
rem Запуск игрового сервера. Окно держи открытым, пока играешь.
rem Сервер слушает ws://localhost:5050/ws
cd /d "%~dp0"

rem Убиваем старый сервер, иначе он держит файлы и блокирует сборку
taskkill /IM Server.exe /F >nul 2>&1

dotnet build server\Server.csproj -v q --nologo
if errorlevel 1 (
  echo.
  echo [ОШИБКА] Сборка не удалась, смотри сообщения выше.
  pause
  exit /b 1
)

echo ============================================
echo   Igra server: ws://localhost:5050/ws
echo   Для остановки закрой это окно или Ctrl+C
echo ============================================
dotnet run --no-build --project server
pause
