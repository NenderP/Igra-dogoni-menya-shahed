using Gacha;
using Server.Game;

namespace Server.Net;

/// <summary>Одно решение бота. null — нечего делать.</summary>
public static class BotBrain
{
    public record Decision(string Kind, string? CharUid = null, string? TargetUid = null, string? CardDefId = null);

    public static Decision? Next(BattleState state, PlayerSide me, Random rng, BotDifficulty difficulty = BotDifficulty.Normal)
    {
        var foe = state.Other(me);
        var active = me.Active;

        if (!active.Alive)
        {
            var alive = me.Alive.FirstOrDefault();
            return alive == null ? null : new Decision("swap", CharUid: alive.Uid);
        }

        // Сложность влияет на агрессию: лёгкий бот часто «зевает» ход
        double actChance = difficulty switch
        {
            BotDifficulty.Easy => 0.45,
            BotDifficulty.Normal => 0.85,
            BotDifficulty.Hard => 1.0,
            _ => 0.85
        };
        if (rng.NextDouble() > actChance)
            return new Decision("end");

        // Ульта — только если накоплена и (на сложном) цель заметно ранена
        if (active.Energy >= active.EnergyMax && (difficulty == BotDifficulty.Hard || rng.NextDouble() < 0.6))
        {
            var t = PickTarget(foe, rng);
            if (t != null) return new Decision("ult", active.Uid, t.Uid);
        }

        if (me.Dice.CanPay(active.Element, BattleEngine.SkillCost))
        {
            var t = PickTarget(foe, rng);
            if (t != null) return new Decision("skill", active.Uid, t.Uid);
        }

        if (me.Hand.Count > 0 && me.Dice.Dice.Count > 0)
        {
            // лёгкий бот редко тратит карты поддержки
            if (difficulty != BotDifficulty.Easy || rng.NextDouble() < 0.4)
                return new Decision("card", CardDefId: me.Hand[rng.Next(me.Hand.Count)]);
        }

        if (!me.FreeSwapUsed)
        {
            var swapTo = me.Alive.FirstOrDefault(c => c.Uid != active.Uid);
            if (swapTo != null) return new Decision("swap", CharUid: swapTo.Uid);
        }

        return new Decision("end");
    }

    private static CharacterState? PickTarget(PlayerSide foe, Random rng) =>
        foe.Alive.OrderBy(_ => rng.Next()).FirstOrDefault();
}
