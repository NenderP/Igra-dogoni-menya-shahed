using Gacha;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Server.Game;
using Server.Net;

// Режимы запуска:
//   dotnet run --project server                — WebSocket-сервер (порт из IGRA_PORT или 5050)
//   dotnet run --project server -- demo        — демо-бой бот vs бот в консоли
//   dotnet run --project server -- sim [N]     — симуляция гачи N круток

if (args.Length > 0 && args[0] == "sim")
{
    int pulls = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 100_000;
    Console.WriteLine(GachaSimulator.Format(GachaSimulator.Run(pulls)));
    return;
}

if (args.Length > 0 && args[0] == "demo")
{
    RunDemo();
    return;
}

RunServer();

// ==================== СЕРВЕР ====================

static void RunServer()
{
    int port = int.TryParse(Environment.GetEnvironmentVariable("IGRA_PORT"), out var p) ? p : 5050;
    var rng = new Random();
    var gacha = new GachaService();
    var matchmaker = new Matchmaker();
    var sessions = new Dictionary<string, GameSession>(); // playerId -> session

    async Task CreateSession(ClientConnection a, ClientConnection b)
    {
        var accA = gacha.GetOrCreate(a.PlayerId!, "playerA");
        var accB = gacha.GetOrCreate(b.PlayerId!, "playerB");
        var session = new GameSession(
            accA.PlayerId, a, DeckFactory.ForPlayer(accA, rng),
            accB.PlayerId, b, DeckFactory.ForPlayer(accB, rng),
            rng, gacha);
        foreach (var id in new[] { accA.PlayerId, accB.PlayerId }) sessions[id] = session;
        await session.StartAsync();
        Console.WriteLine($"[match] {accA.PlayerId} vs {accB.PlayerId}");
    }

    async Task CreateBotSession(ClientConnection human, string difficulty)
    {
        var acc = gacha.GetOrCreate(human.PlayerId!, "player");
        var botDiff = difficulty switch
        {
            "easy" => BotDifficulty.Easy,
            "hard" => BotDifficulty.Hard,
            _ => BotDifficulty.Normal
        };
        var botDeck = new BotDeckGenerator(rng).GetDeck(botDiff);
        var session = new GameSession(
            acc.PlayerId, human, DeckFactory.ForPlayer(acc, rng),
            $"bot_{difficulty}", null, botDeck,
            rng, gacha, botDiff);
        sessions[acc.PlayerId] = session;
        await session.StartAsync();
        Console.WriteLine($"[match] {acc.PlayerId} vs bot ({difficulty})");
    }

    matchmaker.OnPaired += (a, b) => _ = CreateSession(a, b);

    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    var app = builder.Build();

    app.UseWebSockets();
    app.MapGet("/", async (HttpContext ctx) =>
    {
        ctx.Response.ContentType = "text/plain";
        await ctx.Response.WriteAsync("Igra server is running. Connect via /ws");
    });
    app.MapGet("/ws", async (HttpContext ctx) =>
    {
        if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
        var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        var conn = new ClientConnection(ws);
        Console.WriteLine("[conn] клиент подключился");

        while (conn.Connected)
        {
            var msg = await conn.ReceiveAsync();
            if (msg == null) break;

            var parsed = Json.Parse(msg);
            if (parsed == null) { await conn.SendAsync("error", new { code = "bad_json" }); continue; }
            var (type, payload) = parsed.Value;

            try
            {
                switch (type)
                {
                    case "hello":
                    {
                        var id = payload.Str("player_id") ?? Guid.NewGuid().ToString("N")[..8];
                        var name = payload.Str("display_name") ?? "player";
                        var acc = gacha.GetOrCreate(id, name);
                        conn.PlayerId = id;
                        await conn.SendAsync("welcome", new
                        {
                            player_id = acc.PlayerId,
                            display_name = acc.DisplayName,
                            dust = acc.Collection.Dust,
                            collection_size = acc.Collection.Owned.Count
                        });
                        Console.WriteLine($"[hello] {id} ({name})");
                        break;
                    }
                    case "vs_bot":
                        if (conn.PlayerId == null) goto auth;
                        await CreateBotSession(conn, payload.Str("difficulty") ?? "normal");
                        break;
                    case "create_lobby":
                        if (conn.PlayerId == null) goto auth;
                        await conn.SendAsync("lobby_created", new { code = matchmaker.CreateLobby(conn) });
                        break;
                    case "join_lobby":
                        if (conn.PlayerId == null) goto auth;
                        if (!matchmaker.TryJoinLobby(conn, payload.Str("code") ?? ""))
                            await conn.SendAsync("error", new { code = "lobby_not_found" });
                        break;
                    case "find_match":
                        if (conn.PlayerId == null) goto auth;
                        matchmaker.EnqueueRandom(conn);
                        break;
                    case "gacha_pull":
                        if (conn.PlayerId == null) goto auth;
                        await conn.SendAsync("gacha_result",
                            gacha.Pull(conn.PlayerId, Math.Clamp(payload.Int("count", 1), 1, 10)));
                        break;
                    case "collection_sync":
                        if (conn.PlayerId == null) goto auth;
                        await conn.SendAsync("collection_state", gacha.CollectionState(conn.PlayerId));
                        break;
                    case "dust_to_pulls":
                        if (conn.PlayerId == null) goto auth;
                        await conn.SendAsync("dust_exchanged", gacha.DustToPulls(conn.PlayerId, payload.Int("pulls", 1)));
                        break;
                    default:
                    {
                        // действия боя — маршрутизируем в сессию игрока
                        if (conn.PlayerId != null && sessions.TryGetValue(conn.PlayerId, out var session))
                            await session.HandleAsync(conn, type, payload);
                        else
                            await conn.SendAsync("error", new { code = "not_in_battle", message = type });
                        break;
                    }
                    auth:
                    {
                        await conn.SendAsync("error", new { code = "unauthorized", message = "Сначала hello" });
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[err] {type}: {ex.Message}");
                await conn.SendAsync("error", new { code = "server_error", message = ex.Message });
            }
        }

        Console.WriteLine("[conn] клиент отключился");
    });

    Console.WriteLine($"Igra server: ws://0.0.0.0:{port}/ws");
    app.Run($"http://0.0.0.0:{port}");
}

// ==================== ДЕМО-БОЙ ====================

static void RunDemo()
{
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

        if (state.Phase == Phase.Action && state.SideA.EndedTurn && state.SideB.EndedTurn)
            engine.StartRound();
    }

    Console.WriteLine("=== ЛОГ БОЯ ===");
    foreach (var line in state.Log) Console.WriteLine(line);
    Console.WriteLine();
    Console.WriteLine(state.WinnerId != null ? $"Победитель: {state.WinnerId}" : "Бой не завершился за отведённые действия");

    static CharacterState? PickTarget(PlayerSide side) =>
        side.Alive.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
}
