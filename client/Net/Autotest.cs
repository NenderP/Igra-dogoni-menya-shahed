using System.Text.Json;

namespace Igra.Client.Net;

/// <summary>
/// Автотест: подключается к серверу, играет раунд с ботом, крутит гачу — всё без окна.
/// Запуск: dotnet run --project client -- --autotest [--server ws://host:port/ws]
/// </summary>
public static class Autotest
{
    public static async Task<int> RunAsync()
    {
        var net = new NetClient();
        var done = new TaskCompletionSource();
        var messages = new List<(string Type, JsonElement Payload)>();

        Console.WriteLine($"Подключаюсь к {net.ServerUrl}...");
        await net.ConnectAsync();

        string? myUid = null, foeUid = null;

        void Handle(string type, JsonElement p)
        {
            try
            {
                HandleInner(type, p);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!! КРАШ ОБРАБОТКИ {type}: {ex.Message}");
            }
        }

        void HandleInner(string type, JsonElement p)
        {
            Console.WriteLine($"<- {type}");
            messages.Add((type, p));
            switch (type)
            {
                case "welcome":
                    Console.WriteLine($"OK welcome: dust={p.Int("dust")}");
                    _ = net.SendAsync("vs_bot", new { difficulty = "easy" });
                    break;
                case "match_found":
                    Console.WriteLine($"OK match_found: режим={p.Str("mode")}");
                    break;
                case "round_start":
                    Console.WriteLine($"OK round_start: раунд {p.Int("round")}, поддержка: {string.Join(",", p.Arr("support_cards_drawn").EnumerateArray().Select(x => x.GetString()))}");
                    break;
                case "dice_rolled":
                    Console.WriteLine($"OK dice_rolled: {string.Join(",", p.Arr("you").EnumerateArray().Select(x => x.GetString()))}");
                    _ = net.SendAsync("reroll_dice", new { indexes = new[] { 0 } });
                    break;
                case "state_sync":
                    myUid = p.Str("active_character");
                    var foe = p.GetProperty("opponent").Arr("characters").EnumerateArray().FirstOrDefault(c => c.TryGetProperty("alive", out var a) && a.GetBoolean());
                    foeUid = foe.ValueKind != JsonValueKind.Undefined ? foe.Str("uid") : null;
                    if (messages.Count(m => m.Type == "state_sync") == 1)
                        _ = net.SendAsync("use_skill", new { character_uid = myUid, target_uid = foeUid });
                    else
                        _ = net.SendAsync("end_turn", new { });
                    break;
                case "action_result":
                    Console.WriteLine($"OK action_result: {Truncate(p.Str("log"), 90)}");
                    break;
                case "game_over":
                    Console.WriteLine($"OK game_over: победил {p.Str("winner")} (раунд {p.Int("round")})");
                    _ = net.SendAsync("gacha_pull", new { count = 10 });
                    break;
                case "gacha_result":
                    var items = p.Arr("items").EnumerateArray()
                        .Select(i => $"{i.Int("rarity")}★{(i.TryGetProperty("is_new", out var n) && n.GetBoolean() ? "(новый)" : "")}")
                        .ToList();
                    Console.WriteLine($"OK gacha_result: {string.Join(", ", items)} | пыль={p.Int("dust_balance")}");
                    _ = net.SendAsync("collection_sync", new { });
                    break;
                case "collection_state":
                    Console.WriteLine($"OK collection_state: видов={p.Arr("owned").GetArrayLength()}, пыль={p.Int("dust")}");
                    done.TrySetResult();
                    break;
                case "error":
                    Console.WriteLine($"!! error: {p.Str("code")} {p.Str("message")}");
                    break;
            }
        }

        // свой цикл приёма поверх NetClient: подпишемся через поллинг
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested && !done.Task.IsCompleted)
            {
                while (net.TryTake(out var m)) Handle(m.Type, m.Payload);
                await Task.Delay(50);
            }
        });

        await net.SendAsync("hello", new { player_id = "autotest_" + Guid.NewGuid().ToString("N")[..6], display_name = "Autotest" });

        var finished = await Task.WhenAny(done.Task, Task.Delay(Timeout.Infinite, cts.Token));
        Console.WriteLine(finished == done.Task ? "== АВТОТЕСТ ПРОЙДЕН ==" : "== ТАЙМАУТ АВТОТЕСТА ==");
        return finished == done.Task ? 0 : 1;
    }

    private static string Truncate(string? s, int len) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= len ? s : s[..len];
}
