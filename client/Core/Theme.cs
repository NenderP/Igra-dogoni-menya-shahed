using Microsoft.Xna.Framework;

namespace Igra.Client.Core;

/// <summary>
/// Единая палитра стихий — те же оттенки, что рисуются на дайсах в бою
/// (Ru.ElementColor). Один источник правды для UI-компонентов.
/// </summary>
public static class Theme
{
    /// <summary>Затмение — фиолетовый.</summary>
    public static readonly Color Eclipse = new(160, 90, 220);

    /// <summary>Рассвет — тёплое золото.</summary>
    public static readonly Color Dawn = new(250, 200, 120);

    /// <summary>День — оранжевый.</summary>
    public static readonly Color Day = new(248, 158, 68);

    /// <summary>Сумерки — сиреневый.</summary>
    public static readonly Color Twilight = new(150, 116, 190);

    /// <summary>Ночь — синий.</summary>
    public static readonly Color Night = new(84, 104, 190);

    /// <summary>Цвет по ключу стихии персонажа ("dawn"/"day"/"eclipse"/"twilight"/"night").</summary>
    public static Color OfElement(string element) => element switch
    {
        "dawn" => Dawn,
        "day" => Day,
        "eclipse" => Eclipse,
        "twilight" => Twilight,
        "night" => Night,
        _ => Color.Gray
    };
}
