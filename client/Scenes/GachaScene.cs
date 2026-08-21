using FontStashSharp;
using Igra.Client.Core;
using Igra.Client.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace Igra.Client.Scenes;

/// <summary>Экран гачи: крутки, результаты с цветами редкости, коллекция.</summary>
public class GachaScene(IgraGame game) : Scene(game)
{
    private readonly List<string> _lines = new();
    private string _summary = "";

    public override void Draw(SpriteBatch batch, FontSystem fonts)
    {
        G.DrawString(batch, 520, 30, "ГАЧА", Color.Gold, 40);

        if (G.Button(batch, new Rectangle(420, 110, 200, 54), "Крутить ×1"))
            Send("gacha_pull", new { count = 1 });
        if (G.Button(batch, new Rectangle(660, 110, 200, 54), "Крутить ×10"))
            Send("gacha_pull", new { count = 10 });
        if (G.Button(batch, new Rectangle(540, 174, 200, 44), "Коллекция"))
            Send("collection_sync", new { });
        if (G.Button(batch, new Rectangle(20, 20, 120, 44), "← Меню"))
            G.Scene = new MenuScene(G);

        if (_summary.Length > 0)
            G.DrawString(batch, 60, 240, _summary, Color.Yellow, 20);

        var y = 280f;
        foreach (var line in _lines.TakeLast(14))
        {
            var rarity = line.Contains("5★") ? 5 : line.Contains("4★") ? 4 : 3;
            G.DrawString(batch, 60, y, line, IgraGame.RarityColors[rarity], 19);
            y += 26;
        }
    }

    public override void OnMessage(string type, JsonElement payload)
    {
        switch (type)
        {
            case "gacha_result":
                _lines.Clear();
                foreach (var item in payload.Arr("items").EnumerateArray())
                {
                    var defId = item.Str("def_id") ?? "?";
                    var rarity = item.Int("rarity");
                    var isNew = item.TryGetProperty("is_new", out var n) && n.GetBoolean();
                    var dust = item.Int("converted_to_dust");
                    var stars = new string('★', rarity);
                    _lines.Add($"{stars} {defId.Replace("char_", "")}{(isNew ? " — НОВЫЙ!" : $" → +{dust} пыли")}");
                }
                var pity = payload.GetProperty("pity_after");
                _summary = $"Пыль: {payload.Int("dust_balance")} | до гарант-5★: {pity.Int("pulls_since_5star")}/90" +
                           (pity.TryGetProperty("guaranteed_featured", out var g) && g.GetBoolean() ? " (гарант фичера!)" : "");
                break;

            case "collection_state":
                _lines.Clear();
                foreach (var o in payload.Arr("owned").EnumerateArray())
                    _lines.Add($"{o.Str("def_id")}×{o.Int("copies")}");
                _summary = $"Всего видов: {payload.Arr("owned").GetArrayLength()}, пыль: {payload.Int("dust")}";
                break;
        }
    }

    private void Send(string type, object payload) => _ = G.Net.SendAsync(type, payload);
}
