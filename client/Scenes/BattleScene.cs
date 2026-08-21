using FontStashSharp;
using Igra.Client.Core;
using Igra.Client.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace Igra.Client.Scenes;

/// <summary>Боевой экран: враги сверху, мои дайсы/персонажи/рука снизу, кнопки действий. Всё на русском.</summary>
public class BattleScene(IgraGame game) : Scene(game)
{
    private BattleView _view = new();
    private string? _selectedFoeUid;
    private string _log = "";
    private readonly Dictionary<string, int> _dispHp = new();

    private static Color ElColor(string el) => Ru.ElementColor(el);

    public override void OnMessage(string type, JsonElement payload)
    {
        switch (type)
        {
            case "state_sync":
                _view = BattleView.Parse(payload);
                if (_selectedFoeUid == null || _view.FoeChars.All(c => c.Uid != _selectedFoeUid))
                    _selectedFoeUid = _view.FoeChars.FirstOrDefault(c => c.Alive)?.Uid;
                foreach (var c in _view.MyChars.Concat(_view.FoeChars))
                    if (!_dispHp.ContainsKey(c.Uid)) _dispHp[c.Uid] = c.Hp;
                break;
            case "action_result":
            {
                _log = payload.Str("log") ?? "";
                Sfx.Hit();
                var m = System.Text.RegularExpressions.Regex.Match(_log, @"на (\d+)");
                int dmg = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                bool reaction = _log.Contains("Реакция");
                var victim = _view.MyChars.Concat(_view.FoeChars)
                    .FirstOrDefault(c => c.DefId.Length > 0 && _log.Contains(c.DefId));
                if (victim != null)
                {
                    var r = CardRect(victim);
                    var center = new Vector2(r.X + r.Width / 2, r.Y + 20);
                    if (dmg > 0)
                    {
                        Fx.FloatText(center + new Vector2(0, -6), $"-{dmg}",
                            reaction ? Color.Gold : Color.OrangeRed, reaction ? 30 : 24);
                        Fx.Burst(center, Ru.ElementColor(victim.Element), dmg >= 10 ? 22 : 12, dmg >= 10 ? 220 : 150);
                        Fx.Shake(reaction || dmg >= 10 ? 7 : 4);
                        if (reaction) Fx.Flash(Color.Gold, 0.10f);
                    }
                    if (_log.Contains("пал!"))
                    {
                        Sfx.Death();
                        Fx.Burst(new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2), Color.Gray, 26, 190);
                        Fx.Shake(9, 0.3f);
                    }
                }
                break;
            }
            case "round_start":
                _log = $"— Раунд {payload.Int("round")} —";
                break;
            case "game_over":
                var win = payload.Str("winner") == "you";
                if (win) { Sfx.Win(); Fx.Flash(Color.Gold, 0.35f); }
                else { Sfx.Lose(); Fx.Flash(Color.Firebrick, 0.3f); }
                G.Feed.Add(win ? "Победа! Награда начислена." : "Поражение. Есть утешительная пыль.");
                G.Scene = new MenuScene(G);
                break;
        }
    }

    public override void Draw(SpriteBatch batch, FontSystem fonts)
    {
        var target = _selectedFoeUid == null ? null : _view.FoeChars.FirstOrDefault(c => c.Uid == _selectedFoeUid);
        G.DrawString(batch, 20, 10, $"Раунд {_view.Round}", Color.White, 24);
        G.DrawString(batch, 1080, 10, $"Перебросы: {_view.RerollsLeft}", Color.Gray, 16);
        G.DrawString(batch, 360, 12, target != null ? $"Цель: {Ru.Name(target.DefId)}"
            : "Кликни врага, чтобы выбрать цель", target != null ? Color.OrangeRed : Color.Gray, 17);

        DrawSide(batch, _view.FoeChars, 60, true);
        DrawSide(batch, _view.MyChars, 340, false);

        // лог
        G.Panel(batch, new Rectangle(20, 226, 1240, 52), new Color(20, 22, 32), new Color(60, 64, 84));
        G.DrawString(batch, 30, 240, _log.Length > 120 ? _log[..120] : _log, Color.LightGoldenrodYellow, 18);

        // мои дайсы
        for (int i = 0; i < _view.MyDice.Count; i++)
        {
            var el = _view.MyDice[i];
            var r = new Rectangle(30 + i * 62, 292, 54, 54);
            G.Panel(batch, r, ElColor(el), Color.Black);
            G.DrawString(batch, r.X + 12, r.Y + 18, Ru.ElementShort(el), Color.Black, 18);
        }

        // моя рука
        for (int i = 0; i < _view.MyHand.Count; i++)
        {
            var r = new Rectangle(30 + i * 130, 356, 120, 44);
            var hov = r.Contains(Microsoft.Xna.Framework.Input.Mouse.GetState().Position);
            G.Panel(batch, r, hov ? new Color(80, 110, 80) : new Color(60, 80, 60), new Color(110, 140, 110));
            G.DrawString(batch, r.X + 6, r.Y + 12, Ru.SupportRu(_view.MyHand[i]), Color.White, 15);
            if (G.ClickOnce(r)) Send("play_card", new { card_def_id = _view.MyHand[i] });
        }

        // действия
        int by = 600;
        if (G.Button(batch, new Rectangle(700, by, 130, 50), "Скилл")) { Sfx.Skill(); DoSkill(); }
        if (G.Button(batch, new Rectangle(840, by, 130, 50), "Ульта")) { Sfx.Skill(); DoUlt(); }
        if (G.Button(batch, new Rectangle(980, by, 130, 50), "Свап")) DoSwap();
        if (G.Button(batch, new Rectangle(1120, by, 140, 50), "Конец хода", new Color(120, 60, 60)))
            Send("end_turn", new { });
    }

    private void DrawSide(SpriteBatch batch, List<CharView> chars, int y, bool isEnemy)
    {
        float pulse = 0.5f + 0.5f * (float)Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 4);
        for (int i = 0; i < chars.Count; i++)
        {
            var c = chars[i];
            var r = new Rectangle(340 + i * 210, y, 195, 150);
            bool selected = c.Uid == _selectedFoeUid && isEnemy;
            bool isActive = !isEnemy && c.Uid == _view.MyActiveUid;

            G.Panel(batch, r, !c.Alive ? new Color(40, 40, 40) : isEnemy ? new Color(70, 45, 50) : new Color(40, 55, 75),
                isActive ? Color.Gold : selected ? Color.OrangeRed : new Color(60, 64, 84));

            if (isActive && c.Alive)
                G.Panel(batch, new Rectangle(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4), Color.Transparent,
                    new Color((byte)255, (byte)215, (byte)0, (byte)(120 + 100 * pulse)));

            G.DrawString(batch, r.X + 10, r.Y + 8, Ru.Name(c.DefId), Color.White, 17);
            G.DrawString(batch, r.X + 10, r.Y + 30, Ru.ElementRu(c.Element), ElColor(c.Element), 14);
            int rar = Ru.Info(c.DefId)?.Rarity ?? 3;
            G.DrawString(batch, r.X + r.Width - 62, r.Y + 8, new string('★', rar), IgraGame.RarityColors[rar], 15);

            // портрет (если есть картинка в Assets/chars/{def_id}.png)
            var portrait = Art.Portrait(c.DefId);
            if (portrait != null)
                batch.Draw(portrait, new Rectangle(r.X + r.Width - 58, r.Y + 28, 48, 48), Color.White);

            // анимированный HP-бар
            int bw = 175, bh = 16, bx = r.X + 10, byy = r.Y + 56;
            G.FillRect(batch, new Rectangle(bx, byy, bw, bh), new Color(40, 20, 20));
            _dispHp.TryGetValue(c.Uid, out int cur);
            cur += (int)((c.Hp - cur) * 0.2f);
            if (Math.Abs(c.Hp - cur) < 1) cur = c.Hp;
            _dispHp[c.Uid] = cur;
            float frac = c.MaxHp > 0 ? (float)cur / c.MaxHp : 0;
            var hpCol = cur <= c.MaxHp / 3 ? Color.OrangeRed : cur <= c.MaxHp * 2 / 3 ? Color.Gold : Color.LightGreen;
            G.FillRect(batch, new Rectangle(bx, byy, (int)(bw * frac), bh), hpCol);
            G.DrawString(batch, bx + 2, byy, $"{cur}/{c.MaxHp}" + (c.Shield > 0 ? $" (+{c.Shield})" : ""), Color.White, 13);

            // полоска энергии
            int ew = 175;
            G.FillRect(batch, new Rectangle(r.X + 10, r.Y + 80, ew, 8), new Color(20, 20, 40));
            float eFrac = c.EnergyMax > 0 ? (float)c.Energy / c.EnergyMax : 0;
            G.FillRect(batch, new Rectangle(r.X + 10, r.Y + 80, (int)(ew * eFrac), 8),
                c.Energy >= c.EnergyMax ? Color.Gold : new Color(90, 130, 220));
            G.DrawString(batch, r.X + 10, r.Y + 92, $"Энергия {c.Energy}/{c.EnergyMax}",
                c.Energy >= c.EnergyMax ? Color.Gold : Color.Silver, 14);
            if (!c.Alive) G.DrawString(batch, r.X + 50, r.Y + 110, "ПОВЕРЖЕН", Color.Red, 22);

            if (isEnemy && c.Alive && G.ClickOnce(r))
                _selectedFoeUid = c.Uid;
        }
    }

    private Rectangle CardRect(CharView c)
    {
        bool enemy = _view.FoeChars.Contains(c);
        var list = enemy ? _view.FoeChars : _view.MyChars;
        int i = list.IndexOf(c);
        return new Rectangle(340 + Math.Max(i, 0) * 210, enemy ? 60 : 340, 195, 150);
    }

    private void DoSkill()
    {
        if (_view.MyActiveUid.Length == 0) return;
        var t = _view.FoeChars.FirstOrDefault(c => c.Uid == _selectedFoeUid);
        if (t != null)
        {
            var r = CardRect(t);
            Fx.Burst(new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2),
                ElColor(_view.MyChars.FirstOrDefault(x => x.Uid == _view.MyActiveUid)?.Element ?? "day"), 10, 130);
        }
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
}
