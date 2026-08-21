using FontStashSharp;
using Igra.Client.Core;
using Igra.Client.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace Igra.Client.Scenes;

/// <summary>Боевой экран: враги сверху, мои дайсы/персонажи/рука снизу, кнопки действий.</summary>
public class BattleScene(IgraGame game) : Scene(game)
{
    private BattleView _view = new();
    private string? _selectedFoeUid;
    private string _log = "";

    private static Color ElementColor(string el) => el switch
    {
        "dawn" => new Color(250, 200, 120),
        "day" => new Color(250, 240, 150),
        "eclipse" => new Color(160, 90, 220),
        "twilight" => new Color(140, 110, 180),
        "night" => new Color(70, 90, 170),
        _ => Color.Gray
    };

    public override void OnMessage(string type, JsonElement payload)
    {
        switch (type)
        {
            case "state_sync":
                _view = BattleView.Parse(payload);
                if (_selectedFoeUid == null || _view.FoeChars.All(c => c.Uid != _selectedFoeUid))
                    _selectedFoeUid = _view.FoeChars.FirstOrDefault(c => c.Alive)?.Uid;
                break;
            case "action_result":
                _log = payload.Str("log") ?? "";
                break;
            case "round_start":
                _log = $"— Раунд {payload.Int("round")} —";
                break;
            case "game_over":
                var win = payload.Str("winner") == "you";
                G.Feed.Add(win ? "Победа! Награда начислена." : "Поражение. Есть утешительная пыль.");
                G.Scene = new MenuScene(G);
                break;
        }
    }

    public override void Draw(SpriteBatch batch, FontSystem fonts)
    {
        G.DrawString(batch, 20, 12, $"Раунд {_view.Round}", Color.White, 26);
        G.DrawString(batch, 1080, 12, $"Перебросы: {_view.RerollsLeft}", Color.Gray, 18);

        DrawSide(batch, _view.FoeChars, y: 60, isEnemy: true);
        DrawSide(batch, _view.MyChars, y: 330, isEnemy: false);

        // лог
        G.FillRect(batch, new Rectangle(20, 240, 1240, 60), new Color(35, 38, 50));
        G.DrawString(batch, 30, 258, _log.Length > 110 ? _log[..110] : _log, Color.LightGoldenrodYellow, 19);

        // мои дайсы
        for (int i = 0; i < _view.MyDice.Count; i++)
        {
            var r = new Rectangle(30 + i * 62, 510, 54, 54);
            var el = _view.MyDice[i];
            G.FillRect(batch, r, ElementColor(el));
            G.DrawString(batch, r.X + 6, r.Y + 16, el[..3], Color.Black, 17);
        }

        // рука
        for (int i = 0; i < _view.MyHand.Count; i++)
        {
            var r = new Rectangle(30 + i * 130, 580, 120, 44);
            G.Button(batch, r, ShortCard(_view.MyHand[i]), new Color(60, 80, 60), 15);
            if (IgraGame.Clicked(r)) Send("play_card", new { card_def_id = _view.MyHand[i] });
        }

        // кнопки действий
        int by = 650;
        if (G.Button(batch, new Rectangle(700, by, 130, 50), "Скилл")) DoSkill();
        if (G.Button(batch, new Rectangle(840, by, 130, 50), "Ульта")) DoUlt();
        if (G.Button(batch, new Rectangle(980, by, 130, 50), "Свап")) DoSwap();
        if (G.Button(batch, new Rectangle(1120, by, 140, 50), "Конец хода", new Color(120, 60, 60)))
            Send("end_turn", new { });
    }

    private void DrawSide(SpriteBatch batch, List<CharView> chars, int y, bool isEnemy)
    {
        for (int i = 0; i < chars.Count; i++)
        {
            var c = chars[i];
            var r = new Rectangle(340 + i * 210, y, 195, 150);
            bool selected = c.Uid == _selectedFoeUid && isEnemy;
            G.FillRect(batch, r, !c.Alive ? new Color(45, 45, 45)
                : selected ? new Color(90, 60, 40)
                : isEnemy ? new Color(80, 50, 55) : new Color(45, 65, 85));

            G.DrawString(batch, r.X + 10, r.Y + 8, c.ShortName, Color.White, 18);
            G.DrawString(batch, r.X + 10, r.Y + 36, $"{c.Hp}/{c.MaxHp} HP" + (c.Shield > 0 ? $" (+{c.Shield})" : ""),
                c.Hp <= c.MaxHp / 3 ? Color.OrangeRed : Color.LightGreen, 20);
            G.DrawString(batch, r.X + 10, r.Y + 64, $"Энергия {c.Energy}/{c.EnergyMax}",
                c.Energy >= c.EnergyMax ? Color.Gold : Color.Silver, 17);
            G.DrawString(batch, r.X + 10, r.Y + 92, c.Element, ElementColor(c.Element), 17);
            if (!c.Alive) G.DrawString(batch, r.X + 55, r.Y + 55, "ПАЛ", Color.Red, 30);

            if (isEnemy && c.Alive && IgraGame.Clicked(r))
                _selectedFoeUid = c.Uid;
        }
    }

    private void DoSkill()
    {
        var me = _view.MyChars.FirstOrDefault(c => c.Uid == _view.MyActiveUid);
        if (me == null || me.Energy < 1 && !_view.MyDice.Any()) return;
        Send("use_skill", new { character_uid = _view.MyActiveUid, target_uid = _selectedFoeUid ?? "" });
    }

    private void DoUlt()
    {
        Send("use_ultimate", new { character_uid = _view.MyActiveUid, target_uid = _selectedFoeUid ?? "" });
    }

    private void DoSwap()
    {
        var next = _view.MyChars.FirstOrDefault(c => c.Alive && c.Uid != _view.MyActiveUid);
        if (next != null) Send("swap_character", new { to_uid = next.Uid });
    }

    private void Send(string type, object payload) => _ = G.Net.SendAsync(type, payload);

    private static string ShortCard(string defId) =>
        defId.Replace("sup_", "").Replace('_', ' ');
}
