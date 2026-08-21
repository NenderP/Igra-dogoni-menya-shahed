namespace Server.Game;

/// <summary>
/// Пул дайсов игрока на раунд. Бросок — только через движок (server-authoritative).
/// Оплата скиллов: грани стихии персонажа или Omni.
/// </summary>
public class DicePool
{
    public List<TimeOfDay> Dice { get; } = new();

    public void Roll(int count, Random rng)
    {
        Dice.Clear();
        for (int i = 0; i < count; i++) Dice.Add(RollOne(rng));
    }

    public static TimeOfDay RollOne(Random rng)
    {
        // Равные шансы 6 граней — БАЛАНС-TBD (в Геншине omni реже)
        var faces = TimeOfDayExt.RealFaces;
        int r = rng.Next(faces.Length + 1);
        return r == faces.Length ? TimeOfDay.Omni : faces[r];
    }

    public int CountPayable(TimeOfDay element) =>
        Dice.Count(d => d == element || d == TimeOfDay.Omni);

    public bool CanPay(TimeOfDay element, int amount) => CountPayable(element) >= amount;

    /// <summary>Списать amount граней: сначала точные совпадения, потом Omni.</summary>
    public bool Pay(TimeOfDay element, int amount)
    {
        if (!CanPay(element, amount)) return false;
        for (int i = 0; i < amount; i++)
        {
            int exact = Dice.IndexOf(element);
            int idx = exact >= 0 ? exact : Dice.IndexOf(TimeOfDay.Omni);
            Dice.RemoveAt(idx);
        }
        return true;
    }

    /// <summary>Перебросить грани по индексам (один переброс за раунд, как в Геншине).</summary>
    public void Reroll(IEnumerable<int> indexes, Random rng)
    {
        var list = indexes.ToList();
        for (int i = 0; i < list.Count; i++)
            Dice[list[i]] = RollOne(rng);
    }

    /// <summary>Заменить одну грань на Omni (эффект sup_dice_fix).</summary>
    public bool FixToOmni(int index)
    {
        if (index < 0 || index >= Dice.Count || Dice[index] == TimeOfDay.Omni) return false;
        Dice[index] = TimeOfDay.Omni;
        return true;
    }
}
