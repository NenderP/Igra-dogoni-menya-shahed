namespace Gacha;

/// <summary>
/// Генератор рандомных колод для ботов (задача №5).
/// 3 уровня: easy — простые колоды + простой ИИ, normal — средний, hard — комбо + умный ИИ.
/// Колоды всегда рандомные, без дублей персонажей в отряде (3 персонажа).
/// Сервер боя запрашивает через IBotDeckProvider.GetDeck(difficulty) — см. protocol-v0 открытый вопрос №4.
/// </summary>
public enum BotDifficulty { Easy, Normal, Hard }

public record BotDeck(
    BotDifficulty Difficulty,
    string[] Characters,   // 3 def_id, без дублей
    string[] Supports,     // карты поддержки в руке/колоде
    string StrategyHint    // подсказка для ИИ сервера
);

public interface IBotDeckProvider
{
    BotDeck GetDeck(BotDifficulty difficulty);
    BotDeck GetDeck(string difficultyStr) => GetDeck(Parse(difficultyStr));
    static BotDifficulty Parse(string s) => s.ToLower() switch
    {
        "easy" => BotDifficulty.Easy,
        "normal" => BotDifficulty.Normal,
        "hard" => BotDifficulty.Hard,
        _ => BotDifficulty.Normal
    };
}

public class BotDeckGenerator : IBotDeckProvider
{
    private readonly Random _rng;

    // Пулы из /shared/content.md:10
    private static readonly string[] AllChars = new[]
    {
        "char_eclipse_sovereign", "char_dawn_herald",
        "char_day_mage", "char_night_assassin", "char_twilight_trickster", "char_dusk_scout",
        "char_day_squire", "char_night_initiate"
    };

    private static readonly string[] Rarity5 = new[] { "char_eclipse_sovereign", "char_dawn_herald" };
    private static readonly string[] Rarity4 = new[] { "char_day_mage", "char_night_assassin", "char_twilight_trickster", "char_dusk_scout" };
    private static readonly string[] Rarity3 = new[] { "char_day_squire", "char_night_initiate" };

    // Поддержка
    private static readonly string[] SupportsEasy = new[] { "sup_shield_1", "sup_heal_2" };
    private static readonly string[] SupportsHard = new[] { "sup_double_dice", "sup_dice_fix", "sup_energy_boost", "sup_shield_1" };

    // Мета-комбо для hard (пары времён дают реакции — docs/battle-system.md:17)
    // День+Ночь → Сумерки, Рассвет+Затмение → Золотой час и т.д.
    private static readonly string[][] HardCombos = new[]
    {
        new[] { "char_day_mage", "char_night_assassin", "char_twilight_trickster" }, // День+Ночь+Сумерки
        new[] { "char_eclipse_sovereign", "char_dawn_herald", "char_day_mage" },      // Затмение+Рассвет (Золотой час)
        new[] { "char_night_assassin", "char_eclipse_sovereign", "char_dusk_scout" },// Ночь+Затмение
    };

    public BotDeckGenerator(Random? rng = null)
    {
        _rng = rng ?? new Random();
    }

    public BotDeck GetDeck(BotDifficulty difficulty) => difficulty switch
    {
        BotDifficulty.Easy => GenerateEasy(),
        BotDifficulty.Normal => GenerateNormal(),
        BotDifficulty.Hard => GenerateHard(),
        _ => GenerateNormal()
    };

    private BotDeck GenerateEasy()
    {
        // Easy: только 3★ + случайные 4★, без 5★, простая синергия, тупой ИИ
        var pool = Rarity3.Concat(Rarity4).ToArray();
        var chars = PickUnique(pool, 3);
        var sup = PickRandom(SupportsEasy, 2);
        return new BotDeck(BotDifficulty.Easy, chars, sup, "easy: random, no combos, simple AI");
    }

    private BotDeck GenerateNormal()
    {
        // Normal: микс 3★/4★ + 30% шанс одной 5★, умеренная синергия
        var chars = new List<string>();
        bool include5 = _rng.NextDouble() < 0.3;
        if (include5) chars.Add(PickOne(Rarity5));

        var remainingPool = AllChars.Where(c => !chars.Contains(c)).ToArray();
        while (chars.Count < 3) chars.Add(PickOne(remainingPool.Where(c => !chars.Contains(c)).ToArray()));

        Shuffle(chars);
        var sup = PickRandom(SupportsEasy.Concat(SupportsHard).Distinct().ToArray(), 3);
        return new BotDeck(BotDifficulty.Normal, chars.ToArray(), sup, "normal: mixed rarities, light synergy");
    }

    private BotDeck GenerateHard()
    {
        // Hard: 50% взять готовую мету, 50% — собрать синергию с 1–2 ×5★, умный ИИ
        if (_rng.NextDouble() < 0.5)
        {
            var combo = HardCombos[_rng.Next(HardCombos.Length)];
            var sup = PickRandom(SupportsHard, 3);
            return new BotDeck(BotDifficulty.Hard, (string[])combo.Clone(), sup, "hard: meta combo, smart AI");
        }

        // Сборка с упором на 5★ и реакцию
        var chars = new List<string>();
        // гарант минимум одна 5★
        chars.Add(PickOne(Rarity5));
        if (_rng.NextDouble() < 0.5) chars.Add(Rarity5.First(c => !chars.Contains(c)));

        var fillerPool = Rarity4; // добиваем 4★ для силы
        while (chars.Count < 3)
        {
            var pick = PickOne(fillerPool.Where(c => !chars.Contains(c)).ToArray());
            chars.Add(pick);
        }
        Shuffle(chars);
        var supports = PickRandom(SupportsHard, 4);
        return new BotDeck(BotDifficulty.Hard, chars.ToArray(), supports, "hard: 5★ heavy, reaction synergy, smart AI");
    }

    // helpers
    private string PickOne(string[] pool) => pool[_rng.Next(pool.Length)];

    private string[] PickUnique(string[] pool, int count)
    {
        if (count > pool.Length) throw new ArgumentException("count > pool");
        var shuffled = pool.OrderBy(_ => _rng.Next()).ToArray();
        return shuffled.Take(count).ToArray();
    }

    private string[] PickRandom(string[] pool, int count)
    {
        var res = new List<string>();
        for (int i = 0; i < count; i++) res.Add(pool[_rng.Next(pool.Length)]);
        return res.ToArray();
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>Демо-генерация для отладки.</summary>
    public static List<BotDeck> Demo(int perDifficulty = 3, int seed = 123)
    {
        var gen = new BotDeckGenerator(new Random(seed));
        var res = new List<BotDeck>();
        foreach (var d in new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard })
            for (int i = 0; i < perDifficulty; i++) res.Add(gen.GetDeck(d));
        return res;
    }
}
