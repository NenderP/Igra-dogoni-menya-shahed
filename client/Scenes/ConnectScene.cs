using FontStashSharp;
using Igra.Client.Core;
using Igra.Client.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace Igra.Client.Scenes;

public class ConnectScene(IgraGame game) : Scene(game)
{
    public override void Draw(SpriteBatch batch, FontSystem fonts)
    {
        G.DrawString(batch, 480, 320, "Подключение к серверу...", Color.White, 28);
        G.DrawString(batch, 480, 360, G.Net.ServerUrl, Color.Gray, 18);
        if (!string.IsNullOrEmpty(G.StatusLine))
            G.DrawString(batch, 300, 420, G.StatusLine + " (запусти run-server.bat)", Color.OrangeRed, 20);
    }

    public override void OnMessage(string type, JsonElement payload)
    {
        if (type == "welcome")
        {
            G.Feed.Add($"Привет, {payload.Str("display_name")}! Пыль: {payload.Int("dust")}");
            G.Scene = new MenuScene(G);
        }
    }
}
