namespace Server.Game;

/// <summary>Времена суток — грани дайса и "стихии" персонажей (docs/battle-system.md).</summary>
public enum TimeOfDay
{
    Dawn,      // Рассвет
    Day,       // День
    Eclipse,   // Затмение
    Twilight,  // Сумерки
    Night,     // Ночь
    Omni       // Универсальная грань
}

public static class TimeOfDayExt
{
    public static bool IsReal(this TimeOfDay t) => t != TimeOfDay.Omni;

    public static string Ru(this TimeOfDay t) => t switch
    {
        TimeOfDay.Dawn => "Рассвет",
        TimeOfDay.Day => "День",
        TimeOfDay.Eclipse => "Затмение",
        TimeOfDay.Twilight => "Сумерки",
        TimeOfDay.Night => "Ночь",
        _ => "Универсал"
    };

    public static readonly TimeOfDay[] RealFaces =
        { TimeOfDay.Dawn, TimeOfDay.Day, TimeOfDay.Eclipse, TimeOfDay.Twilight, TimeOfDay.Night };
}
