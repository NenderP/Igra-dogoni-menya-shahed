using Gacha;
using Server.Game;

// Режимы запуска:
//   dotnet run --project server            — демо-бой бот vs бот
//   dotnet run --project server -- sim     — симуляция гачи 100k круток (отчёт по пити)

if (args.Length > 0 && args[0] == "sim")
{
    int pulls = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 100_000;
    Console.WriteLine(GachaSimulator.Format(GachaSimulator.Run(pulls)));
    return;
}

// Демо-прогон: бот easy vs бот hard, случайные легальные действия.
// Проверка каркаса движка боя (задача №3). Сеть подключим следующим шагом.

var rng = new Random(20260822);
var provider = new BotDeckGenerator(rng);

var deckA = provider.GetDeck(BotDifficulty.Easy);
var deckB = provider.GetDeck(BotDifficulty.Hard);

Console.WriteLine($"Колода A (easy): {string.Join(", ", deckA.Characters)}");
Console.WriteLine($"Колода B (hard): {string.Join(", ", deckB.Characters)}");
Console.WriteLine();

var engine = new BattleEngine("BotEasy", "BotHard", deckA, deckB, rng);
var state = engine.State;

engine.StartRound();
int guard = 0;

while (state.Phase != Phase.GameOver && guard++ < 500)
{
    foreach (var side in new[] { state.SideA, state.SideB })
    {
        if (state.Phase == Phase.GameOver) break;
        if (side.EndedTurn || !side.Alive.Any()) continue;

        var active = side.Active;

        // приоритет: ульта → скилл → поддержка → свап → конец хода
        if (active.Energy >= active.EnergyMax)
        {
            var target = PickTarget(state.Other(side));
            if (target != null && engine.UseUltimate(side, active, target)) continue;
        }

        bool paid = false;
        for (int tries = 0; tries < 6 && !paid; tries++)
        {
            var target = PickTarget(state.Other(side));
            if (target != null) paid = engine.UseSkill(side, active, target);
        }
        if (paid) continue;

        if (side.Hand.Count > 0)
        {
            var card = side.Hand[rng.Next(side.Hand.Count)];
            if (engine.PlaySupport(side, card)) continue;
        }

        if (!side.FreeSwapUsed)
        {
            var swapTo = side.Alive.FirstOrDefault(c => c.Uid != active.Uid);
            if (swapTo != null && engine.Swap(side, swapTo.Uid)) continue;
        }

        engine.EndTurn(side);
    }

    // если оба закончили — EndRound вызван внутри EndTurn, стартуем новый раунд
    if (state.Phase == Phase.Action && state.SideA.EndedTurn && state.SideB.EndedTurn)
        engine.StartRound();
}

Console.WriteLine("=== ЛОГ БОЯ ===");
foreach (var line in state.Log) Console.WriteLine(line);
Console.WriteLine();
Console.WriteLine(state.WinnerId != null ? $"Победитель: {state.WinnerId}" : "Бой не завершился за отведённые действия");
return;

static CharacterState? PickTarget(PlayerSide side) =>
    side.Alive.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
