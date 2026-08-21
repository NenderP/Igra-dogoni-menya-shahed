namespace Gacha;

/// <summary>
/// Константы гачи из README.md: шансы, пити, пыль. Меняет баланс — плейтестами.
/// </summary>
public static class GachaConfig
{
    // Шансы (README: 5★=0.6%, 4★=5.1%, 3★=94.3%)
    public const double Rate5Star = 0.006;
    public const double Rate4Star = 0.051;
    public const double Rate3Star = 0.943; // 1 - 0.006 - 0.051

    // Пити (README: хард 90, софт с 75)
    public const int HardPity5Star = 90;
    public const int SoftPityStart = 75;
    // 4★ гарант каждые 10 (стандарт Геншина, в README не указан, делаем как в Геншине)
    public const int HardPity4Star = 10;

    // Софт-пити: линейный рост шанса с SoftPityStart до HardPity
    // Формула: base + (pulls_since - SoftPityStart + 1) * SoftPityStep
    // Подобрано так, чтобы к 89 шанс был ~ 50%, к 90 = 100% (хард)
    public const double SoftPityStep = 0.06; // +6% за каждый пулл после 75

    // Пыль за дубль (README: 3★=5, 4★=25, 5★=100)
    public const int Dust3Star = 5;
    public const int Dust4Star = 25;
    public const int Dust5Star = 100;

    // Крафт крутки пылью: 50–75 пыли = 1 крутка (README), дефолт 60
    public const int DustPerPull = 60;
    public const int DustPerPullMin = 50;
    public const int DustPerPullMax = 75;

    // 50/50: при выпадении 5★ — 50% фичер, иначе гарант следующего 5★ фичером
    public const double FeaturedWinRate = 0.5;

    // Награды за бой (заглушка, генерирует сервер боя, зачисляет гача-модуль)
    public const int RewardDustWin = 15;
    public const int RewardDustLose = 5;
    public const int RewardPullsWin = 3;
    public const int RewardPullsLose = 1;
}
