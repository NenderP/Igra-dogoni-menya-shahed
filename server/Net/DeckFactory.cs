using Gacha;
using Server.Game;

namespace Server.Net;

/// <summary>Собирает BotDeck из коллекции игрока: топ-3 по редкости + стартовые добивки.</summary>
public static class DeckFactory
{
    private static readonly string[] Starters = { "char_day_squire", "char_night_initiate" };
    private static readonly string[] Filler4 = { "char_day_mage", "char_night_assassin", "char_twilight_trickster", "char_dusk_scout" };
    private static readonly string[] StarterSupports = { "sup_shield_1", "sup_heal_2", "sup_energy_boost" };

    public static BotDeck ForPlayer(Account acc, Random rng)
    {
        var owned = acc.Collection.Owned.Keys.ToList();
        var chars = owned
            .Where(id => CharacterCatalog.GetOrNull(id) != null)
            .Distinct()
            .OrderByDescending(id => CharacterCatalog.Get(id).Rarity)
            .Take(3)
            .ToList();

        foreach (var s in Starters)
            if (chars.Count < 3 && !chars.Contains(s)) chars.Add(s);
        foreach (var f in Filler4.OrderBy(_ => rng.Next()))
        {
            if (chars.Count >= 3) break;
            if (!chars.Contains(f)) chars.Add(f);
        }

        return new BotDeck(BotDifficulty.Normal, chars.ToArray(),
            StarterSupports.ToArray(), $"player deck: {acc.DisplayName}");
    }

    /// <summary>Колода из явно выбранных 3 персонажей (экран выбора отряда на клиенте).</summary>
    public static BotDeck FromDefIds(IReadOnlyList<string> ids)
    {
        var valid = ids.Where(id => CharacterCatalog.GetOrNull(id) != null).Distinct().Take(3).ToList();
        while (valid.Count < 3)
        {
            var filler = Starters.Concat(Filler4).FirstOrDefault(s => !valid.Contains(s));
            if (filler == null) break;
            valid.Add(filler);
        }
        return new BotDeck(BotDifficulty.Normal, valid.ToArray(),
            StarterSupports.ToArray(), "player chosen deck");
    }
}
