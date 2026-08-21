using FontStashSharp;
using Igra.Client.Core;
using Igra.Client.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace Igra.Client.Scenes;

/// <summary>Выбор отряда: 3 персонажа из 8, с описанием и статистикой.</summary>
public class DeckSelectScene(IgraGame game) : Scene(game)
{
    private readonly List<Ru.CharInfo> _pool = Ru.Characters;
    private readonly HashSet<string> _sel = new();
    private string _diff = "normal";
    private string _notice = "";

    public override void Draw(SpriteBatch batch, FontSystem fonts)
    {
        G.DrawString(batch, 430, 36, "ВЫБЕРИ ОТРЯД (3 персонажа)", Color.Gold, 34);

        int cols = 4, cw = 285, ch = 210, gx = 20, gy = 22, x0 = 40, y0 = 96;
        for (int i = 0; i < _pool.Count; i++)
        {
            var c = _pool[i];
            int col = i % cols, row = i / cols;
            var r = new Rectangle(x0 + col * (cw + gx), y0 + row * (ch + gy), cw, ch);
            bool selected = _sel.Contains(c.DefId);

            G.Panel(batch, r, selected ? new Color(40, 60, 90) : new Color(28, 30, 42),
                selected ? Color.Gold : new Color(60, 64, 84));
            G.DrawString(batch, r.X + 12, r.Y + 10, c.Name, Color.White, 19);
            G.DrawString(batch, r.X + 12, r.Y + 36, new string('★', c.Rarity), IgraGame.RarityColors[c.Rarity], 17);
            G.DrawString(batch, r.X + 190, r.Y + 36, Ru.ElementRu(c.Element), Ru.ElementColor(c.Element), 16);
            G.DrawString(batch, r.X + 12, r.Y + 60, $"Здоровье: {c.Hp}", Color.LightGreen, 16);
            var lines = Wrap(c.Desc, cw - 24, 15);
            for (int k = 0; k < lines.Count; k++)
                G.DrawString(batch, r.X + 12, r.Y + 84 + k * 20, lines[k], Color.LightGray, 15);
            if (selected)
                G.DrawString(batch, r.X + cw - 34, r.Y + 8, "✓", Color.Gold, 26);

            if (G.ClickOnce(r) && (selected || _sel.Count < 3))
            {
                if (selected) _sel.Remove(c.DefId); else _sel.Add(c.DefId);
                Sfx.Click();
            }
        }

        // сложность
        int by = 560;
        G.DrawString(batch, 40, by - 4, "Сложность бота:", Color.White, 20);
        if (G.Button(batch, new Rectangle(240, by - 8, 130, 44), "Лёгкий", _diff == "easy" ? Color.Green : null)) _diff = "easy";
        if (G.Button(batch, new Rectangle(380, by - 8, 140, 44), "Обычный", _diff == "normal" ? Color.Green : null)) _diff = "normal";
        if (G.Button(batch, new Rectangle(530, by - 8, 140, 44), "Сложный", _diff == "hard" ? Color.Green : null)) _diff = "hard";

        // старт
        bool ready = _sel.Count == 3;
        if (G.Button(batch, new Rectangle(900, by - 8, 340, 52),
                ready ? "НАЧАТЬ БОЙ (3/3)" : $"НУЖНО ЕЩЁ {3 - _sel.Count}", ready ? Color.DarkGreen : new Color(70, 70, 80), 22))
        {
            if (ready)
            {
                Sfx.Start();
                Send("vs_bot", new { difficulty = _diff, characters = _sel.ToList() });
            }
        }

        if (G.Button(batch, new Rectangle(20, 20, 120, 40), "← Меню"))
            G.Scene = new MenuScene(G);

        if (_notice.Length > 0)
            G.DrawString(batch, 40, 624, _notice, Color.OrangeRed, 18);
    }

    public override void OnMessage(string type, JsonElement payload)
    {
        switch (type)
        {
            case "match_found":
                G.Scene = new BattleScene(G);
                break;
            case "error":
                _notice = "Ошибка: " + (payload.Str("message") ?? payload.Str("code") ?? "?");
                break;
        }
    }

    private static List<string> Wrap(string text, float maxW, float size)
    {
        var outLines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' '))
        {
            var test = line.Length == 0 ? word : line + " " + word;
            if (test.Length * size * 0.5f > maxW && line.Length > 0)
            {
                outLines.Add(line);
                line = word;
            }
            else line = test;
        }
        if (line.Length > 0) outLines.Add(line);
        return outLines;
    }

    private void Send(string type, object payload) => _ = G.Net.SendAsync(type, payload);
}
