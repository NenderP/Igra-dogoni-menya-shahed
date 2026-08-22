using FontStashSharp;
using Igra.Client.Core;
using Igra.Client.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace Igra.Client.Scenes;

/// <summary>
/// Экран коллекции: сетка всех персонажей игры.
/// Собранные — портрет, имя, стихия; несобранные — тёмная карточка с «?».
/// </summary>
public class CollectionScene(IgraGame game) : Scene(game)
{
    private readonly HashSet<string> _owned = new();
    private bool _inited;
    private int _dust, _pulls;
    private string _status = "";

    public override void Draw(SpriteBatch batch, FontSystem fonts)
    {
        if (!_inited) { _inited = true; Send("collection_sync", new { }); }

        if (G.Button(batch, new Rectangle(20, 20, 120, 44), "← Меню"))
            G.Scene = new MenuScene(G);

        G.DrawString(batch, 520, 30, "КОЛЛЕКЦИЯ", Color.Gold, 40);
        G.DrawString(batch, 470, 78, $"Собрано: {_owned.Count} из {Ru.Characters.Count}",
            _owned.Count == Ru.Characters.Count ? Color.LightGreen : Color.Silver, 20);

        string status = $"Пыль: {_dust}   Круток: {_pulls}";
        float sw = G.Measure(status, 18).X;
        G.DrawString(batch, 1280 - 16 - sw, 74, status, Color.Gray, 18);

        // сетка карточек
        int cols = 4, cardW = 220, cardH = 270, gapX = 28, gapY = 30;
        int x0 = (1280 - (cols * cardW + (cols - 1) * gapX)) / 2;
        int y0 = 116;

        for (int i = 0; i < Ru.Characters.Count; i++)
        {
            var c = Ru.Characters[i];
            int col = i % cols, row = i / cols;
            var r = new Rectangle(x0 + col * (cardW + gapX), y0 + row * (cardH + gapY), cardW, cardH);
            bool have = _owned.Contains(c.DefId);

            if (!have)
            {
                // несобранный: тёмная загадочная карточка
                G.Panel(batch, r, new Color(28, 28, 34), new Color(70, 70, 80));
                var qs = G.Measure("?", 64);
                G.DrawString(batch, r.X + (cardW - qs.X) / 2, r.Y + 62, "?", new Color(85, 85, 95), 64);
                string un = "???";
                float uw = G.Measure(un, 16).X;
                G.DrawString(batch, r.X + (cardW - uw) / 2, r.Y + 190, un, new Color(100, 100, 112), 16);
                continue;
            }

            // собранный: рамка цвета стихии, портрет, имя
            var elCol = Ru.ElementColor(c.Element);
            G.Panel(batch, r, new Color(40, 55, 75), elCol);
            if (c.Rarity >= 4)
                G.Panel(batch, Inflate(r, 3), Color.Transparent,
                    IgraGame.RarityColors[c.Rarity] * 0.5f);

            var portrait = Art.Portrait(c.DefId);
            if (portrait != null)
                batch.Draw(portrait, new Rectangle(r.X + (cardW - 148) / 2, r.Y + 22, 148, 148), Color.White);

            var st = G.Measure(new string('★', c.Rarity), 15);
            G.DrawString(batch, r.X + (cardW - st.X) / 2, r.Y + 176,
                new string('★', c.Rarity), IgraGame.RarityColors[c.Rarity], 15);

            var nm = G.Measure(c.Name, 16);
            G.DrawString(batch, r.X + (cardW - nm.X) / 2, r.Y + 198, c.Name, Color.White, 16);

            var elT = Ru.ElementRu(c.Element);
            var ew = G.Measure(elT, 13);
            G.DrawString(batch, r.X + (cardW - ew.X) / 2, r.Y + 226, elT, elCol, 13);
        }
    }

    private static Rectangle Inflate(Rectangle r, int d) =>
        new(r.X - d, r.Y - d, r.Width + 2 * d, r.Height + 2 * d);

    public override void OnMessage(string type, JsonElement payload)
    {
        switch (type)
        {
            case "collection_state":
                _owned.Clear();
                foreach (var o in payload.Arr("owned").EnumerateArray())
                {
                    var id = o.Str("def_id");
                    if (id != null) _owned.Add(id);
                }
                _dust = payload.Int("dust");
                _pulls = payload.Int("pulls");
                break;
            case "error":
                _status = payload.Str("message") ?? payload.Str("code") ?? "?";
                break;
        }
    }

    private void Send(string type, object payload) => _ = G.Net.SendAsync(type, payload);
}
