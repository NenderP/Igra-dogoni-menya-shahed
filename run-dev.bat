@echo off
chcp 65001 >nul
rem Запуск игры на ПК для тестов: сервер в фоне + клиент в окне.
cd /d "%~dp0"

taskkill /IM Server.exe /F >nul 2>&1

dotnet build server\Server.csproj -v q --nologo
if errorlevel 1 (
  echo [ОШИБКА] Сборка сервера не удалась, см. выше.
  pause
  exit /b 1
)

start "" dotnet run --no-build --project server
dotnet run --project client
pause
