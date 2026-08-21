using FontStashSharp;
using Igra.Client.Core;
using Igra.Client.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace Igra.Client.Scenes;

/// <summary>Экран гачи: крутки, результаты с цветами редкости, коллекция, крафт пылью.</summary>
public class GachaScene(IgraGame game) : Scene(game)
{
    private readonly List<(int Rarity, string DefId, bool IsNew, int Dust)> _items = new();
    private string _summary = "";
    private bool _inited;
    private int _pullsLeft;
    private int _dust;

    public override void Draw(SpriteBatch batch, FontSystem fonts)
    {
        if (!_inited) { _inited = true; Send("collection_sync", new { }); }

        G.DrawString(batch, 520, 30, "ГАЧА", Color.Gold, 40);
        G.DrawString(batch, 20, 36, $"Круток: {_pullsLeft}   Пыль: {_dust}", Color.LightGray, 20);

        if (G.Button(batch, new Rectangle(380, 110, 200, 54), "Крутить ×1"))
            TryPull(1);
        if (G.Button(batch, new Rectangle(600, 110, 200, 54), "Крутить ×10"))
            TryPull(10);
        if (G.Button(batch, new Rectangle(380, 176, 200, 40), "Крафт ×1 (60 пыли)"))
            Send("dust_to_pulls", new { pulls = 1 });
        if (G.Button(batch, new Rectangle(600, 176, 200, 40), "Крафт ×10 (600)"))
            Send("dust_to_pulls", new { pulls = 10 });
        if (G.Button(batch, new Rectangle(20, 20, 120, 44), "← Меню"))
            G.Scene = new MenuScene(G);

        if (_summary.Length > 0)
            G.DrawString(batch, 60, 232, _summary, Color.YellowGreen, 19);

        // карточки результата
        int cardW = 150, cardH = 70, x0 = 60, y0 = 280, gap = 12;
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            int col = i % 7, row = i / 7;
            var r = new Rectangle(x0 + col * (cardW + gap), y0 + row * (cardH + gap), cardW, cardH);
            G.FillRect(batch, r, IgraGame.RarityColors[it.Rarity]);
            G.DrawString(batch, r.X + 8, r.Y + 8, new string('★', it.Rarity), Color.White, 18);
            G.DrawString(batch, r.X + 8, r.Y + 34, it.DefId.Replace("char_", ""), Color.White, 16);
            G.DrawString(batch, r.X + 8, r.Y + 52,
                it.IsNew ? "НОВЫЙ!" : $"+{it.Dust} пыли", it.IsNew ? Color.White : Color.LightGray, 16);
        }
    }

    private void TryPull(int count)
    {
        if (_pullsLeft < (count == 10 ? 10 : 1) && _pullsLeft < 1)
        {
            _summary = "Нет круток — скрафти пылью (60 пыли = 1)";
            return;
        }
        Send("gacha_pull", new { count });
    }

    public override void OnMessage(string type, JsonElement payload)
    {
        switch (type)
        {
            case "gacha_result":
                _items.Clear();
                foreach (var item in payload.Arr("items").EnumerateArray())
                {
                    _items.Add((
                        item.Int("rarity"),
                        item.Str("def_id") ?? "?",
                        item.TryGetProperty("is_new", out var n) && n.GetBoolean(),
                        item.Int("converted_to_dust")
                    ));
                }
                _pullsLeft = payload.Int("pulls_left");
                _dust = payload.Int("dust_balance");
                var pity = payload.GetProperty("pity_after");
                int since = pity.Int("pulls_since_5star");
                bool feat = pity.TryGetProperty("guaranteed_featured", out var g) && g.GetBoolean();
                _summary = $"Пыль: {_dust} | круток: {_pullsLeft} | до гарант-5★: {since}/90" + (feat ? " (гарант фичера!)" : "");
                break;

            case "dust_exchanged":
                _dust = payload.Int("dust_balance");
                _pullsLeft = payload.Int("pulls_left");
                _summary = $"Сделано круток: {payload.Int("pulls_granted")}. Теперь круток: {_pullsLeft}";
                break;

            case "collection_state":
                _items.Clear();
                foreach (var o in payload.Arr("owned").EnumerateArray())
                    _items.Add((0, o.Str("def_id") ?? "?", false, 0));
                _dust = payload.Int("dust");
                _pullsLeft = payload.Int("pulls");
                _summary = $"Коллекция: {payload.Arr("owned").GetArrayLength()} видов | пыль: {_dust} | круток: {_pullsLeft}";
                break;

            case "error":
                _summary = "Ошибка: " + (payload.Str("message") ?? payload.Str("code") ?? "?");
                break;
        }
    }

    private void Send(string type, object payload) => _ = G.Net.SendAsync(type, payload);
}
