using FontStashSharp;
using Igra.Client.Core;
using Igra.Client.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace Igra.Client.Scenes;

public class MenuScene(IgraGame game) : Scene(game)
{
    private string _notice = "";

    public override void Draw(SpriteBatch batch, FontSystem fonts)
    {
        G.DrawString(batch, 440, 40, "ИГРА — карточная", Color.Gold, 42);

        var y = 140f;
        foreach (var line in G.Feed.TakeLast(6))
        {
            G.DrawString(batch, 60, y, line, Color.LightGray, 18);
            y += 26;
        }

        if (G.Button(batch, new Rectangle(490, 200, 300, 54), "Собрать отряд", new Color(60, 70, 120)))
            G.Scene = new DeckSelectScene(G);

        if (G.Button(batch, new Rectangle(490, 260, 300, 54), "Бой с лёгким ботом"))
            Send("vs_bot", new { difficulty = "easy" });
        if (G.Button(batch, new Rectangle(490, 324, 300, 54), "Бой с обычным ботом"))
            Send("vs_bot", new { difficulty = "normal" });
        if (G.Button(batch, new Rectangle(490, 388, 300, 54), "Бой со сложным ботом"))
            Send("vs_bot", new { difficulty = "hard" });
        if (G.Button(batch, new Rectangle(490, 452, 145, 54), "Гача"))
            G.Scene = new GachaScene(G);
        if (G.Button(batch, new Rectangle(645, 452, 145, 54), "Коллекция"))
        {
            Send("collection_sync", new { });
            _notice = "Загружаю коллекцию...";
        }

        if (_notice.Length > 0)
            G.DrawString(batch, 60, 530, _notice, Color.Yellow, 20);
        G.DrawString(batch, 60, 660, $"player_id: {G.PlayerId}", Color.DimGray, 16);
    }

    public override void OnMessage(string type, JsonElement payload)
    {
        switch (type)
        {
            case "match_found":
                G.Scene = new BattleScene(G);
                break;
            case "collection_state":
                var owned = payload.Arr("owned");
                _notice = $"Коллекция: {owned.GetArrayLength()} видов персонажей, пыль: {payload.Int("dust")}, " +
                          $"пити 5★: {payload.GetProperty("pity").Int("pulls_since_5star")}/90";
                break;
            case "error":
                _notice = "Ошибка: " + (payload.Str("message") ?? payload.Str("code") ?? "?");
                break;
        }
    }

    private void Send(string type, object payload) => _ = G.Net.SendAsync(type, payload);
}
