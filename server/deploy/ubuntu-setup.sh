#!/usr/bin/env bash
# Автонастройка убунту-ноута как игрового сервера.
# Запуск: bash ubuntu-setup.sh
set -e

echo "=== 1/4: ставлю .NET SDK 8 и git ==="
sudo apt update
sudo apt install -y dotnet-sdk-8.0 git

echo "=== 2/4: забираю код ==="
if [ ! -d "$HOME/Igra-dogoni-menya-shahed" ]; then
    git clone https://github.com/NenderP/Igra-dogoni-menya-shahed.git ~/Igra-dogoni-menya-shahed
else
    echo "папка уже есть, пропускаю клон"
fi
cd ~/Igra-dogoni-menya-shahed
git pull

echo "=== 3/4: собираю сервер ==="
dotnet build server/Server.csproj -v q

echo "=== 4/4: проверочный запуск демо-боя ==="
dotnet run --project server -- demo | tail -n 5

cat <<'EOF'

=================================================
Готово! Управление сервером:

  запустить сервер:      cd ~/Igra-dogoni-menya-shahed && dotnet run --project server
  демо-бой в консоли:    dotnet run --project server -- demo
  симуляция гачи:        dotnet run --project server -- sim

Сервер будет слушать ws://0.0.0.0:5050/ws
(доступ из интернета настроим туннелем следующим шагом)
=================================================
EOF
