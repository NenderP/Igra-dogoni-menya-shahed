namespace Gacha;

/// <summary>
/// Симулятор гачи для проверки шансов и пити на большой выборке.
/// Запуск: dotnet run --project server -- sim [кол-во круток]
/// </summary>
public static class GachaSimulator
{
    public record SimReport(
        int Pulls,
        int Count3, int Count4, int Count5,
        double Pct3, double Pct4, double Pct5,
        double AvgPullsPer5Star, int MinPullsPer5Star, int MaxPullsPer5Star,
        int FiveStarsFromSoftPity, int FiveStarsFromHardPity);

    public static SimReport Run(int pulls = 100_000, int seed = 42)
    {
        var rng = new Random(seed);
        var sys = new GachaSystem(rng);
        var col = new PlayerCollection();

        int c3 = 0, c4 = 0, c5 = 0;
        var gaps = new List<int>();
        int softZone = 0, hardZone = 0;

        for (int i = 0; i < pulls; i++)
        {
            int gapIfFive = col.PullsSince5Star + 1; // сколько круток прошло с прошлой 5★, если сейчас выпадет 5★
            var res = sys.Pull(col, 1);
            switch (res.Items[0].Rarity)
            {
                case 3: c3++; break;
                case 4: c4++; break;
                case 5:
                    c5++;
                    gaps.Add(gapIfFive);
                    if (gapIfFive >= GachaConfig.HardPity5Star) hardZone++;
                    else if (gapIfFive >= GachaConfig.SoftPityStart) softZone++;
                    break;
            }
        }

        return new SimReport(
            pulls, c3, c4, c5,
            100.0 * c3 / pulls, 100.0 * c4 / pulls, 100.0 * c5 / pulls,
            gaps.Count > 0 ? gaps.Average() : 0,
            gaps.Count > 0 ? gaps.Min() : 0,
            gaps.Count > 0 ? gaps.Max() : 0,
            softZone, hardZone);
    }

    public static string Format(SimReport r) => $"""
        === Симуляция гачи: {r.Pulls:N0} круток ===
        Редкости:      3★ {r.Pct3:F2}% ({r.Count3:N0}) | 4★ {r.Pct4:F2}% ({r.Count4:N0}) | 5★ {r.Pct5:F2}% ({r.Count5:N0})
        5★ всего:      {r.Count5:N0}
          средний интервал: {r.AvgPullsPer5Star:F1} круток
          мин/макс:         {r.MinPullsPer5Star} / {r.MaxPullsPer5Star}
          из софт-пити (75+): {r.FiveStarsFromSoftPity} | с хард-пити (90): {r.FiveStarsFromHardPity}
        Эталон Геншина: 3★ 94.3% | 4★ 5.1% | 5★ ~1.6% (с учётом пити)
        """;
}
