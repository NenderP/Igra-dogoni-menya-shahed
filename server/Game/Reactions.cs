namespace Server.Game;

/// <summary>
/// Реакции: пары времён дают чистый бонус урона (без дебаффов), docs/battle-system.md.
/// Резолв по паре (стихия атакующего, стихия защищающегося).
/// </summary>
public static class Reactions
{
    public readonly record struct Reaction(string Name, int BonusDamage, double Multiplier);

    public static Reaction? Resolve(TimeOfDay attacker, TimeOfDay defender)
    {
        if (!attacker.IsReal() || !defender.IsReal()) return null;

        return (attacker, defender) switch
        {
            (TimeOfDay.Day, TimeOfDay.Night) or (TimeOfDay.Night, TimeOfDay.Day) => new Reaction("Сумерки", 2, 1.0),
            (TimeOfDay.Dawn, TimeOfDay.Eclipse) or (TimeOfDay.Eclipse, TimeOfDay.Dawn) => new Reaction("Золотой час", 0, 2.0),
            (TimeOfDay.Night, TimeOfDay.Eclipse) or (TimeOfDay.Eclipse, TimeOfDay.Night) => new Reaction("Полная тьма", 2, 1.0),
            (TimeOfDay.Twilight, TimeOfDay.Dawn) or (TimeOfDay.Dawn, TimeOfDay.Twilight) => new Reaction("Заря", 2, 1.0),
            _ => null
        };
    }
}
