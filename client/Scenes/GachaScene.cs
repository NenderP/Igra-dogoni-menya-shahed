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
    /// <summary>Минимальный отступ контента от краёв экрана.</summary>
    private const int Margin = 16;

    private readonly List<(int Rarity, string DefId, bool IsNew, int Dust)> _items = new();
    private readonly List<(int Rarity, string DefId, bool IsNew, int Dust)> _pending = new();
    private readonly List<float> _ages = new();
    private float _revealTimer;
    private string _summary = "";
    private bool _inited;
    private int _pullsLeft;
    private int _dust;

    public override void Draw(SpriteBatch batch, FontSystem fonts)
    {
        if (!_inited) { _inited = true; Send("collection_sync", new { }); }

        UpdateReveal();

        G.DrawString(batch, 520, 30, "ГАЧА", Color.Gold, 40);

        // баланс в правом верхнем углу (правое выравнивание, не пересекается с кнопкой «← Меню»)
        string status = $"Круток: {_pullsLeft}   Пыль: {_dust}";
        float sw = G.Measure(status, 20).X;
        G.DrawString(batch, 1280 - Margin - sw, 14, status, Color.LightGray, 20);

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

        // карточки результата (с pop-анимацией, портретом и иерархией редкости)
        int cardW = 150, cardH = 76, x0 = 60, y0 = 280, gap = 12;
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            float age = _ages.Count > i ? _ages[i] : 1f;
            float k = Math.Clamp(age * 6f, 0f, 1f);
            int col = i % 7, row = i / 7;
            int w = (int)(cardW * (0.4f + 0.6f * k));
            int h = (int)(cardH * k);
            var r = new Rectangle(x0 + col * (cardW + gap), y0 + row * (cardH + gap), w, h);

            G.FillRect(batch, r, IgraGame.RarityColors[it.Rarity]);

            // иерархия редкости
            if (h >= 20 && w >= cardW - 4)
            {
                if (it.Rarity >= 4)
                {
                    // свечение вокруг редкой карточки
                    var rc = IgraGame.RarityColors[it.Rarity];
                    G.Panel(batch, Inflate(r, 4), Color.Transparent, rc * 0.45f);
                    G.Panel(batch, Inflate(r, 9), Color.Transparent, rc * 0.22f);
                    if (it.Rarity == 5)
                        G.Panel(batch, Inflate(r, 15), Color.Transparent, rc * 0.12f);
                    // толстая цветная рамка (двойной контур)
                    G.Panel(batch, Inflate(r, 2), Color.Transparent, rc);
                    G.Panel(batch, r, Color.Transparent, rc);
                }
                else
                {
                    // обычная: тонкая серая рамка
                    G.Panel(batch, r, Color.Transparent, new Color(116, 120, 130));
                }
            }

            if (h < 40) continue;

            // портрет слева (если есть пиксель-арт)
            var portrait = Art.Portrait(it.DefId);
            int tx = r.X + 8;
            if (portrait != null)
            {
                batch.Draw(portrait, new Rectangle(r.X + 6, r.Y + (h - Math.Min(56, h - 8)) / 2, 56, Math.Min(56, h - 8)), Color.White);
                tx = r.X + 68;
            }
            int tw = r.X + r.Width - tx - 4;

            G.DrawString(batch, tx, r.Y + 6, new string('★', Math.Max(it.Rarity, 0)), Color.White, 13);
            string nm = Fit(G, Ru.Name(it.DefId), 14, tw);
            G.DrawString(batch, tx, r.Y + 24, nm, Color.White, 14);
            G.DrawString(batch, tx, r.Y + 46,
                it.IsNew ? "НОВЫЙ!" : $"+{it.Dust} пыли", it.IsNew ? Color.White : new Color(230, 230, 235), 13);
        }
    }

    private static Rectangle Inflate(Rectangle r, int d) =>
        new(r.X - d, r.Y - d, r.Width + 2 * d, r.Height + 2 * d);

    /// <summary>Обрезает строку под ширину, добавляя «…».</summary>
    private static string Fit(IgraGame g, string s, float size, float maxW)
    {
        if (g.Measure(s, size).X <= maxW) return s;
        while (s.Length > 1 && g.Measure(s + "…", size).X > maxW)
            s = s[..^1];
        return s + "…";
    }

    private void UpdateReveal()
    {
        for (int i = 0; i < _ages.Count; i++) _ages[i] += G.Dt;

        if (_pending.Count == 0) return;
        _revealTimer -= G.Dt;
        while (_revealTimer <= 0 && _pending.Count > 0)
        {
            var it = _pending[0];
            _pending.RemoveAt(0);
            _items.Add(it);
            _ages.Add(0);

            int col = (_items.Count - 1) % 7, row = (_items.Count - 1) / 7;
            var center = new Vector2(60 + col * 162 + 75, 280 + row * 88 + 38);
            Fx.Burst(center, IgraGame.RarityColors[it.Rarity], it.Rarity == 3 ? 8 : 20, it.Rarity == 3 ? 90 : 180);

            if (it.Rarity == 5) { Sfx.Epic(); Fx.Flash(Color.Gold, 0.28f); Fx.Shake(8); }
            else if (it.Rarity == 4) { Sfx.Rare(); Fx.Flash(new Color(150, 90, 220), 0.12f); }
            else Sfx.Pull();

            _revealTimer += 0.33f;
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
                _ages.Clear();
                _pending.Clear();
                foreach (var item in payload.Arr("items").EnumerateArray())
                {
                    _pending.Add((
                        item.Int("rarity"),
                        item.Str("def_id") ?? "?",
                        item.TryGetProperty("is_new", out var n) && n.GetBoolean(),
                        item.Int("converted_to_dust")
                    ));
                }
                _revealTimer = 0.2f;
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
                _ages.Clear();
                _pending.Clear();
                foreach (var o in payload.Arr("owned").EnumerateArray())
                    _items.Add((0, o.Str("def_id") ?? "?", false, 0));
                for (int i = 0; i < _items.Count; i++) _ages.Add(1f);
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
