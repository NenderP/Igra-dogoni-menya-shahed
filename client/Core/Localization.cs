using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Igra.Client.Core;

/// <summary>Русская локализация и справочник персонажей (зеркало server/Game/CharacterCatalog.cs).</summary>
public static class Ru
{
    public record CharInfo(string DefId, string Name, string Element, int Rarity, int Hp, string Desc);

    public static readonly List<CharInfo> Characters = new()
    {
        new("char_eclipse_sovereign", "Владыка Затмения",   "eclipse_sovereign", 5, 30, "Повелитель теней. Бьёт сквозь любой свет."),
        new("char_dawn_herald",       "Вестник Рассвета",    "dawn_herald",       5, 28, "Несёт первый луч и слепит врагов."),
        new("char_day_mage",          "Маг Дня",             "day_mage",          4, 24, "Жжёт врагов солнечным огнём."),
        new("char_night_assassin",    "Убийца Ночи",         "night_assassin",    4, 24, "Бьёт из тьмы без предупреждения."),
        new("char_twilight_trickster","Трюкач Сумерек",      "twilight_trickster",4, 22, "Путает стихии, обожает реакции."),
        new("char_dusk_scout",        "Следопыт Заката",     "dusk_scout",        4, 22, "Быстрый разведчик сумерек."),
        new("char_day_squire",        "Оруженосец Дня",      "day_squire",        3, 18, "Честный боец рассвета."),
        new("char_night_initiate",    "Послушник Ночи",      "night_initiate",    3, 18, "Ученик тёмных искусств."),
    };

    private static readonly Dictionary<string, CharInfo> ByDef =
        Characters.ToDictionary(c => c.DefId);

    public static CharInfo? Info(string defId) => ByDef.TryGetValue(defId, out var c) ? c : null;

    public static string Name(string defId) => Info(defId)?.Name ?? defId.Replace("char_", "");

    private static readonly Dictionary<string, string> Elements = new()
    {
        ["dawn"] = "Рассвет", ["day"] = "День", ["eclipse"] = "Затмение",
        ["twilight"] = "Сумерки", ["night"] = "Ночь", ["omni"] = "Универсал"
    };

    public static string ElementRu(string key) => Elements.TryGetValue(key, out var v) ? v : key;
    public static string ElementShort(string key) => key switch
    {
        "dawn" => "Рас", "day" => "Ден", "eclipse" => "Зат",
        "twilight" => "Сум", "night" => "Ноч", "omni" => "Уни", _ => key[..2]
    };

    private static readonly Dictionary<string, string> Supports = new()
    {
        ["sup_shield_1"] = "Щит I",
        ["sup_double_dice"] = "Второй бросок",
        ["sup_heal_2"] = "Лечение II",
        ["sup_energy_boost"] = "Прилив энергии",
        ["sup_dice_fix"] = "Фиксация грани",
    };

    public static string SupportRu(string defId) => Supports.TryGetValue(defId, out var v) ? v : defId.Replace("sup_", "");

    /// <summary>Все id карт поддержки — для подмены в логах.</summary>
    public static readonly System.Collections.Generic.IReadOnlyList<string> SupportIds =
        Supports.Keys.ToList();

    /// <summary>
    /// Единая палитра стихий (те же хексы, что красят дайсы):
    /// Затмение — фиолетовый, Рассвет — золото, День — оранжевый,
    /// Сумерки — сиреневый, Ночь — синий.
    /// </summary>
    public static Color ElementColor(string key) => key switch
    {
        "dawn" => new Color(250, 200, 120),
        "day" => new Color(248, 158, 68),
        "eclipse" => new Color(160, 90, 220),
        "twilight" => new Color(150, 116, 190),
        "night" => new Color(84, 104, 190),
        _ => Color.Gray
    };
}
