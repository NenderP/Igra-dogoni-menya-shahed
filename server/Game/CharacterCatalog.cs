namespace Server.Game;

/// <summary>
/// Справочник персонажей и карт поддержки. Зеркало /shared/content.md (владелец — модуль gacha).
/// TODO v1: генерировать из общего JSON, чтобы не дублировать ручками.
/// </summary>
public static class CharacterCatalog
{
    public record Def(string DefId, int Rarity, TimeOfDay Element, int Hp, int EnergyMax);

    private static readonly Dictionary<string, Def> Defs = new()
    {
        ["char_eclipse_sovereign"] = new("char_eclipse_sovereign", 5, TimeOfDay.Eclipse, 12, 3),
        ["char_dawn_herald"]       = new("char_dawn_herald",       5, TimeOfDay.Dawn,    11, 3),
        ["char_day_mage"]          = new("char_day_mage",          4, TimeOfDay.Day,     10, 2),
        ["char_night_assassin"]    = new("char_night_assassin",    4, TimeOfDay.Night,   10, 2),
        ["char_twilight_trickster"]= new("char_twilight_trickster",4, TimeOfDay.Twilight, 9, 2),
        ["char_dusk_scout"]        = new("char_dusk_scout",        4, TimeOfDay.Twilight, 9, 2),
        ["char_day_squire"]        = new("char_day_squire",        3, TimeOfDay.Day,      8, 2),
        ["char_night_initiate"]    = new("char_night_initiate",    3, TimeOfDay.Night,    8, 2),
    };

    /// <summary>Базовая атака по редкости — БАЛАНС-TBD.</summary>
    public static int DefaultAttack(int rarity) => rarity >= 5 ? 3 : 2;

    public static Def Get(string defId) =>
        Defs.TryGetValue(defId, out var d) ? d : throw new KeyNotFoundException($"Нет персонажа {defId} в справочнике");
}

public enum SupportEffect { Shield, Heal, EnergyBoost, ExtraReroll, FixDie }

/// <summary>Карты поддержки из /shared/content.md. Стоимость розыгрыша — 1 любой дайс (БАЛАНС-TBD).</summary>
public static class SupportCatalog
{
    public record Def(string DefId, SupportEffect Effect, int Amount);

    private static readonly Dictionary<string, Def> Defs = new()
    {
        ["sup_shield_1"]     = new("sup_shield_1",     SupportEffect.Shield,      2),
        ["sup_double_dice"]  = new("sup_double_dice",  SupportEffect.ExtraReroll, 2),
        ["sup_heal_2"]       = new("sup_heal_2",       SupportEffect.Heal,        2),
        ["sup_energy_boost"] = new("sup_energy_boost", SupportEffect.EnergyBoost, 1),
        ["sup_dice_fix"]     = new("sup_dice_fix",     SupportEffect.FixDie,      1),
    };

    public static readonly string[] AllIds = Defs.Keys.ToArray();

    public static Def Get(string defId) =>
        Defs.TryGetValue(defId, out var d) ? d : throw new KeyNotFoundException($"Нет карты {defId} в справочнике");
}
