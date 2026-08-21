using Server.Game;

namespace Server.Net;

/// <summary>Одно решение бота. null — нечего делать.</summary>
public static class BotBrain
{
    public record Decision(string Kind, string? CharUid = null, string? TargetUid = null, string? CardDefId = null);

    public static Decision? Next(BattleState state, PlayerSide me, Random rng)
    {
        var foe = state.Other(me);
        var active = me.Active;

        if (!active.Alive)
        {
            var alive = me.Alive.FirstOrDefault();
            return alive == null ? null : new Decision("swap", CharUid: alive.Uid);
        }

        if (active.Energy >= active.EnergyMax)
        {
            var t = RandomTarget(foe, rng);
            if (t != null) return new Decision("ult", active.Uid, t.Uid);
        }

        if (me.Dice.CanPay(active.Element, BattleEngine.SkillCost))
        {
            var t = RandomTarget(foe, rng);
            if (t != null) return new Decision("skill", active.Uid, t.Uid);
        }

        if (me.Hand.Count > 0 && me.Dice.Dice.Count > 0)
            return new Decision("card", CardDefId: me.Hand[rng.Next(me.Hand.Count)]);

        if (!me.FreeSwapUsed)
        {
            var swapTo = me.Alive.FirstOrDefault(c => c.Uid != active.Uid);
            if (swapTo != null) return new Decision("swap", CharUid: swapTo.Uid);
        }

        return new Decision("end");
    }

    private static CharacterState? RandomTarget(PlayerSide foe, Random rng) =>
        foe.Alive.OrderBy(_ => rng.Next()).FirstOrDefault();
}
