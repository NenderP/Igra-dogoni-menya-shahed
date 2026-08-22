using FontStashSharp;
using Igra.Client.Core;
using Igra.Client.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace Igra.Client.Scenes;

/// <summary>
/// Боевой экран в компоновке Genius Invokation TCG:
/// соперник сверху, мой отряд по центру, дайсы слева-снизу (кликабельны для переброса),
/// веер карт справа-снизу, колонка действий справа. Иконки стихий — из Assets/icons.
/// </summary>
public class BattleScene(IgraGame game) : Scene(game)
{
    private BattleView _view = new();
    private string? _selectedFoeUid;
    private string _log = "";
    private readonly Dictionary<string, int> _dispHp = new();
    private readonly HashSet<int> _selDice = new();

    private const int CardW = 170;
    private static Color ElColor(string el) => Ru.ElementColor(el);

    private Texture2D? Orb(string el) => Art.Tex("icons" + System.IO.Path.DirectorySeparatorChar + $"el_{el}.png");

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
                var raw = payload.Str("log") ?? "";
                Sfx.Hit();

                // урон/жертву определяем по сырому логу (там технические id)
                var m = System.Text.RegularExpressions.Regex.Match(raw, @"на (\d+)");
                int dmg = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                bool reaction = raw.Contains("Реакция");
                var victim = _view.MyChars.Concat(_view.FoeChars)
                    .FirstOrDefault(c => c.DefId.Length > 0 && raw.Contains(c.DefId));
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
                    if (raw.Contains("пал!"))
                    {
                        Sfx.Death();
                        Fx.Burst(new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2), Color.Gray, 26, 190);
                        Fx.Shake(9, 0.3f);
                    }
                }

                // игроку показываем лог без технических идентификаторов
                _log = PrettifyLog(raw);
                break;
            }

            case "round_start":
                _log = $"— Раунд {payload.Int("round")} —";
                _selDice.Clear();
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
        // ===== верхняя полоса =====
        // номер раунда показан один раз — на плашке-разделителе между командами
        var target = _selectedFoeUid == null ? null : _view.FoeChars.FirstOrDefault(c => c.Uid == _selectedFoeUid);
        G.DrawString(batch, 300, 10, target != null ? $"Цель: {Ru.Name(target.DefId)}"
            : "Кликни врага = выбрать цель", target != null ? Color.OrangeRed : Color.Gray, 17);
        G.DrawString(batch, 1005, 10, $"Рука соперника: {_view.FoeHandCount}", Color.Silver, 16);
        G.DrawString(batch, 1180, 10, $"Перебросы: {_view.RerollsLeft}", Color.Gray, 16);

        // ===== ряд соперника (верх, по центру) =====
        DrawSide(batch, _view.FoeChars, y: 48, h: 130, isEnemy: true);
        // маркер активного соперника
        var foeAct = _view.FoeChars.FirstOrDefault(c => c.Uid == _view.FoeActiveUid);
        if (foeAct != null && foeAct.Alive)
        {
            var fr = CardRect(foeAct);
            G.FillRect(batch, new Rectangle(fr.X + fr.Width / 2 - 6, fr.Y - 12, 12, 12), Color.Red);
        }

        // ===== разделитель между командами: постоянный бейдж раунда + строка лога =====
        var badgeR = new Rectangle(20, 186, 160, 42);
        G.Panel(batch, badgeR, new Color(32, 36, 54), new Color(120, 110, 60));
        string rt = $"Раунд {_view.Round}";
        var rs = G.Measure(rt, 20);
        G.DrawString(batch, badgeR.X + (badgeR.Width - rs.X) / 2, badgeR.Y + (badgeR.Height - rs.Y) / 2 + 1,
            rt, Color.Gold, 20);

        var logR = new Rectangle(190, 186, 1070, 42);
        G.Panel(batch, logR, new Color(20, 22, 32), new Color(60, 64, 84));
        string shownLog = _log.Length > 100 ? _log[..100] : _log;
        G.DrawString(batch, logR.X + 12, 194, shownLog, Color.LightGoldenrodYellow, 17);

        // ===== мой отряд (центр) =====
        DrawSide(batch, _view.MyChars, y: 240, h: 160, isEnemy: false);

        // ===== дайсы (слева-снизу, сетка 4х2, клик = выбор) =====
        G.DrawString(batch, 30, 408, "Дайсы", Color.Silver, 15);
        for (int i = 0; i < _view.MyDice.Count; i++)
        {
            int colI = i % 4, rowI = i / 4;
            var el = _view.MyDice[i];
            var r = new Rectangle(30 + colI * 66, 428 + rowI * 66, 58, 58);
            bool picked = _selDice.Contains(i);
            var baseC = ElColor(el);

            // кубик: скруглённый квадрат, рамка на два тона светлее заливки
            if (picked)
                batch.Draw(G.RoundTex, Inflate(r, 6), Color.White * 0.85f);
            batch.Draw(G.RoundTex, Inflate(r, 3), picked ? Color.White : Color.Lerp(baseC, Color.White, 0.45f));
            batch.Draw(G.RoundTex, r, baseC * (picked ? 1f : 0.82f));

            // пиктограмма стихии в центре
            var orb = Orb(el);
            if (orb != null)
            {
                int isz = 36;
                batch.Draw(orb, new Rectangle(r.X + (r.Width - isz) / 2, r.Y + (r.Height - isz) / 2, isz, isz),
                    picked ? Color.White : Color.White * 0.95f);
            }
            else
            {
                G.DrawString(batch, r.X + 16, r.Y + 38, Ru.ElementShort(el),
                    picked ? Color.Black : new Color(20, 20, 26), 14);
            }

            if (G.ClickOnce(r))
            {
                if (!picked && _selDice.Count < _view.RerollsLeft * 8) { _selDice.Add(i); Sfx.Click(); }
                else if (picked) _selDice.Remove(i);
            }
        }

        // кнопка переброса
        bool canReroll = _view.RerollsLeft > 0 && _selDice.Count > 0;
        if (G.Button(batch, new Rectangle(30, 566, 256, 44),
                canReroll ? $"Перебросить ({_selDice.Count})" : "Переброс не доступен",
                canReroll ? new Color(80, 90, 140) : new Color(60, 60, 70), 18))
        {
            Send("reroll_dice", new { indexes = _selDice.ToArray() });
            _selDice.Clear();
        }

        // ===== веер руки (справа-снизу) =====
        for (int i = 0; i < _view.MyHand.Count; i++)
        {
            var r = new Rectangle(745 + i * 56, 556, 112, 150);
            var hov = r.Contains(Microsoft.Xna.Framework.Input.Mouse.GetState().Position);
            G.Panel(batch, r, hov ? new Color(78, 104, 78) : new Color(58, 76, 58),
                hov ? new Color(150, 190, 150) : new Color(105, 130, 105));
            G.DrawString(batch, r.X + 8, r.Y + 10, Ru.SupportRu(_view.MyHand[i]), Color.White, 15);
            G.DrawString(batch, r.X + 8, r.Y + 120, "1 любой дайс", Color.LightGray, 12);
            if (G.ClickOnce(r)) { Sfx.Card(); Send("play_card", new { card_def_id = _view.MyHand[i] }); }
        }
        if (_view.MyHand.Count == 0)
            G.DrawString(batch, 900, 620, "Рука пуста", Color.DimGray, 16);

        // ===== колонка действий (справа) =====
        int ax = 1148, aw = 114;
        if (G.Button(batch, new Rectangle(ax, 252, aw, 50), "Скилл")) { Sfx.Skill(); DoSkill(); }
        if (G.Button(batch, new Rectangle(ax, 310, aw, 50), "Ульта")) { Sfx.Skill(); DoUlt(); }
        if (G.Button(batch, new Rectangle(ax, 368, aw, 50), "Свап")) DoSwap();
        if (G.Button(batch, new Rectangle(ax, 426, aw, 50), "Конец хода", new Color(120, 60, 60)))
            Send("end_turn", new { });
    }

    private void DrawSide(SpriteBatch batch, List<CharView> chars, int y, int h, bool isEnemy)
    {
        int gap = 20;
        int x0 = (1280 - (chars.Count * CardW + (chars.Count - 1) * gap)) / 2;
        float pulse = 0.5f + 0.5f * (float)Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 4);

        for (int i = 0; i < chars.Count; i++)
        {
            var c = chars[i];
            var r = new Rectangle(x0 + i * (CardW + gap), y, CardW, h);
            bool selected = c.Uid == _selectedFoeUid && isEnemy;
            bool isActive = isEnemy ? c.Uid == _view.FoeActiveUid : c.Uid == _view.MyActiveUid;

            // рамка карточки = цвет стихии
            var elBorder = c.Alive ? ElColor(c.Element) : new Color(60, 60, 60);
            G.Panel(batch, r,
                !c.Alive ? new Color(40, 40, 40) : isEnemy ? new Color(70, 45, 50) : new Color(40, 55, 75),
                elBorder);

            // активный/выбранный — внешний контур поверх рамки стихии
            if (isActive && c.Alive)
                G.Panel(batch, new Rectangle(r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6), Color.Transparent,
                    new Color((byte)255, (byte)215, (byte)0, (byte)(110 + 110 * pulse)));
            else if (selected && isEnemy)
                G.Panel(batch, new Rectangle(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4), Color.Transparent,
                    Color.OrangeRed);

            // портрет слева
            var portrait = Art.Portrait(c.DefId);
            int tx = r.X + 8;
            if (portrait != null)
            {
                batch.Draw(portrait, new Rectangle(r.X + 6, r.Y + 6, 52, 52), Color.White);
                tx = r.X + 64;
            }
            G.DrawString(batch, tx, r.Y + 8, Ru.Name(c.DefId), Color.White, 15);
            G.DrawString(batch, tx, r.Y + 28, Ru.ElementRu(c.Element), ElColor(c.Element), 13);
            int rar = Ru.Info(c.DefId)?.Rarity ?? 3;
            G.DrawString(batch, tx, r.Y + 46, new string('★', rar), IgraGame.RarityColors[rar], 13);
            var orb = Orb(c.Element);
            if (orb != null) batch.Draw(orb, new Rectangle(r.X + r.Width - 24, r.Y + 6, 18, 18), Color.White);

            // HP-бар анимированный
            int bw = CardW - 20, bh = 15, bx = r.X + 10, byy = r.Y + 64;
            G.FillRect(batch, new Rectangle(bx, byy, bw, bh), new Color(40, 20, 20));
            _dispHp.TryGetValue(c.Uid, out int cur);
            cur += (int)((c.Hp - cur) * 0.2f);
            if (Math.Abs(c.Hp - cur) < 1) cur = c.Hp;
            _dispHp[c.Uid] = cur;
            float frac = c.MaxHp > 0 ? (float)cur / c.MaxHp : 0;
            var hpCol = cur <= c.MaxHp / 3 ? Color.OrangeRed : cur <= c.MaxHp * 2 / 3 ? Color.Gold : Color.LightGreen;
            G.FillRect(batch, new Rectangle(bx, byy, (int)(bw * frac), bh), hpCol);
            G.DrawString(batch, bx + 2, byy, $"{cur}/{c.MaxHp}" + (c.Shield > 0 ? $" (+{c.Shield})" : ""), Color.White, 12);

            // энергия
            G.FillRect(batch, new Rectangle(bx, byy + 20, bw, 7), new Color(20, 20, 40));
            float eFrac = c.EnergyMax > 0 ? (float)c.Energy / c.EnergyMax : 0;
            G.FillRect(batch, new Rectangle(bx, byy + 20, (int)(bw * eFrac), 7),
                c.Energy >= c.EnergyMax ? Color.Gold : new Color(90, 130, 220));
            G.DrawString(batch, bx, byy + 30, $"Энергия {c.Energy}/{c.EnergyMax}",
                c.Energy >= c.EnergyMax ? Color.Gold : Color.Silver, 14);

            if (!c.Alive) G.DrawString(batch, r.X + 42, r.Y + h / 2 + 6, "ПОВЕРЖЕН", Color.Red, 20);

            if (isEnemy && c.Alive && G.ClickOnce(r))
                _selectedFoeUid = c.Uid;
        }
    }

    /// <summary>
    /// Убирает из лога технические идентификаторы: char_*/sup_* → русские имена,
    /// id игрока → «Ты», bot_* → «Бот». Под IGRA_DEBUG лог остаётся сырым.
    /// </summary>
    private string PrettifyLog(string log)
    {
        if (Core.DebugConfig.Enabled) return log;

        foreach (var c in Ru.Characters)
            log = log.Replace(c.DefId, c.Name);
        foreach (var s in Ru.SupportIds)
            log = log.Replace(s, Ru.SupportRu(s));
        if (!string.IsNullOrEmpty(G.PlayerId))
            log = log.Replace(G.PlayerId, "Ты");
        log = System.Text.RegularExpressions.Regex.Replace(log, @"bot_\w+", "Бот");
        return log;
    }

    private static Rectangle Inflate(Rectangle r, int d) =>
        new(r.X - d, r.Y - d, r.Width + 2 * d, r.Height + 2 * d);

    private Rectangle CardRect(CharView c)
    {
        bool enemy = _view.FoeChars.Contains(c);
        var list = enemy ? _view.FoeChars : _view.MyChars;
        int n = list.Count;
        int x0 = (1280 - (n * CardW + Math.Max(n - 1, 0) * 20)) / 2;
        int i = Math.Max(list.IndexOf(c), 0);
        return new Rectangle(x0 + i * (CardW + 20), enemy ? 48 : 240, CardW, enemy ? 130 : 160);
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
        if (next != null) { Sfx.SwapSnd(); Send("swap_character", new { to_uid = next.Uid }); }
    }

    private void Send(string type, object payload) => _ = G.Net.SendAsync(type, payload);
}
