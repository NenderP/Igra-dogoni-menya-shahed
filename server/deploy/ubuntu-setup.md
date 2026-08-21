# Развёртывание сервера на Ubuntu-ноуте

> Пошагово, выполняется один раз. Сервер пока консольный (демо/симуляция),
> WebSocket-хост добавляем следующим шагом — тогда же и туннель пригодится.

## 1. .NET SDK

```bash
sudo apt update && sudo apt install -y dotnet-sdk-8.0
dotnet --version   # должно быть 8.x
```

## 2. Код

```bash
sudo apt install -y git
git clone https://github.com/NenderP/Igra-dogoni-menya-shahed.git
cd Igra-dogoni-menya-shahed
```

## 3. Проверка (то же самое, что run-demo.bat на Windows)

```bash
dotnet run --project server          # демо-бой ботов
dotnet run --project server -- sim   # симуляция гачи
```

## 4. Прод-сборка

```bash
dotnet publish server -c Release -o ~/igra-server
~/igra-server/Server                  # запускается собранное
```

## 5. Автозапуск через systemd (когда появится сетевой хост)

`/etc/systemd/system/igra.service`:

```ini
[Unit]
Description=Igra card game server
After=network.target

[Service]
WorkingDirectory=/home/ИМЯ/igra-server
ExecStart=/home/ИМЯ/igra-server/Server
Restart=always
User=ИМЯ

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now igra
journalctl -u igra -f   # смотреть логи
```

## 6. Доступ из интернета — туннель (когда будет WebSocket)

Вариант А — Cloudflare Tunnel (стабильный адрес, нужен свой домен на Cloudflare):

```bash
curl -L https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64 -o cloudflared
chmod +x cloudflared && sudo mv cloudflared /usr/local/bin/
cloudflared tunnel login
cloudflared tunnel create igra
# конфиг: маршрут на localhost:PORT, автозапуск сервисом
```

Вариант Б — ngrok (быстрый старт, есть бесплатный статичный домен):

```bash
snap install ngrok
ngrok config add-authtoken ТОКЕН_С_САЙТА_NGROK
ngrok http PORT   # выдаст публичный https-адрес
```

Порт сервера слушаем на `0.0.0.0`, туннель пробрасывает наружу по HTTPS/WSS.
