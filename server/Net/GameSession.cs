using System.Text.Json;
using Gacha;
using Server.Game;

namespace Server.Net;

/// <summary>
/// Сессия одного боя: движок + соединения игроков (+ бот при необходимости).
/// Рассылает персональный state_sync каждому, ведёт ходы бота.
/// </summary>
public class GameSession
{
    private readonly BattleEngine _engine;
    private readonly Dictionary<string, ClientConnection> _conns = new();
    private readonly Dictionary<string, PlayerSide> _sides = new();
    private readonly Random _rng;
    private readonly GachaService _gacha;
    private readonly string? _botPlayerId;
    private readonly BotDifficulty _botDifficulty = BotDifficulty.Normal;
    private bool _gameOverSent;

    public GameSession(string idA, ClientConnection connA, BotDeck deckA,
                       string idB, ClientConnection? connB, BotDeck deckB,
                       Random rng, GachaService gacha, BotDifficulty botDifficulty = BotDifficulty.Normal)
    {
        _rng = rng;
        _gacha = gacha;
        _botDifficulty = botDifficulty;
        if (connB == null) _botPlayerId = idB;

        _engine = new BattleEngine(idA, idB, deckA, deckB, rng);
        _conns[idA] = connA; connA.PlayerId = idA; _sides[idA] = _engine.State.SideA;
        if (connB != null) { _conns[idB] = connB; connB.PlayerId = idB; }
        _sides[idB] = _engine.State.SideB;
    }

    public async Task StartAsync()
    {
        var (idA, idB) = (_engine.State.SideA.PlayerId, _engine.State.SideB.PlayerId);
        await SendTo(idA, "match_found", new
        {
            mode = _botPlayerId != null ? "bot" : "duel",
            opponent = new { id = idB, name = idB },
            you_go_first = true
        });
        if (_conns.TryGetValue(idB, out var cb))
            await SendTo(idB, "match_found", new
            {
                mode = "duel",
                opponent = new { id = idA, name = idA },
                you_go_first = false
            });

        await BeginRound();
    }

    // ---------- Входящие действия ----------

    public async Task HandleAsync(ClientConnection conn, string type, JsonElement p)
    {
        var side = SideOf(conn);
        if (side == null || State.Phase == Phase.GameOver) return;

        bool ok = type switch
        {
            "reroll_dice" => _engine.Reroll(side, p.IntArray("indexes")),
            "play_card" => _engine.PlaySupport(side, p.Str("card_def_id") ?? "", p.TryGetProperty("die_index", out var di) ? di.GetInt32() : null),
            "use_skill" => ActOn(p, (a, d) => _engine.UseSkill(side, a, d)),
            "use_ultimate" => ActOn(p, (a, d) => _engine.UseUltimate(side, a, d)),
            "swap_character" => _engine.Swap(side, p.Str("to_uid") ?? ""),
            "end_turn" => _engine.EndTurn(side),
            _ => false
        };

        if (!ok && type != "end_turn")
        {
            await conn.SendAsync("error", new { code = "illegal_action", message = $"Не удалось выполнить {type}" });
            return;
        }

        if (State.Phase == Phase.GameOver) { await FinishGame(); return; }

        // Бот отвечает только после того, как человек закончил ход
        if (type == "end_turn")
        {
            if (_botPlayerId != null)
            {
                var botSide = _sides[_botPlayerId];
                if (!botSide.EndedTurn) await RunBot(botSide);
                if (State.Phase == Phase.GameOver) { await FinishGame(); return; }
            }

            if (State.SideA.EndedTurn && State.SideB.EndedTurn && State.Phase == Phase.Action)
                await BeginRound();
            else
                await SyncAll();
        }
        else
        {
            await SendLastLogAsync(conn.PlayerId!, conn.PlayerId!);
            await SyncAll();
        }
    }

    private bool ActOn(JsonElement p, Func<CharacterState, CharacterState, bool> act)
    {
        var attackerUid = p.Str("character_uid");
        var targetUid = p.Str("target_uid");
        var side = _sides.Values.First(s => s.Characters.Any(c => c.Uid == attackerUid));
        var attacker = side.Characters.FirstOrDefault(c => c.Uid == attackerUid);
        var defender = FindChar(targetUid ?? "");
        return attacker != null && defender != null && act(attacker, defender);
    }

    // ---------- Бот ----------

    private async Task RunBot(PlayerSide bot)
    {
        int guard = 0;
        while (!bot.EndedTurn && State.Phase == Phase.Action && guard++ < 20)
        {
            var decision = BotBrain.Next(State, bot, _rng, _botDifficulty);
            if (decision == null) { _engine.EndTurn(bot); break; }

            switch (decision.Kind)
            {
                case "ult":
                    if (!_engine.UseUltimate(bot, FindOwn(bot, decision.CharUid!), FindChar(decision.TargetUid!)!))
                        _engine.EndTurn(bot);
                    break;
                case "skill":
                    if (!_engine.UseSkill(bot, FindOwn(bot, decision.CharUid!), FindChar(decision.TargetUid!)!))
                        _engine.EndTurn(bot);
                    break;
                case "card":
                    if (!_engine.PlaySupport(bot, decision.CardDefId!)) _engine.EndTurn(bot);
                    break;
                case "swap":
                    if (!_engine.Swap(bot, decision.CharUid!)) _engine.EndTurn(bot);
                    break;
                default:
                    _engine.EndTurn(bot);
                    break;
            }
            await SendLastLogAsync(_botPlayerId!, _sides.Keys.First(id => id != _botPlayerId));
        }
    }

    // ---------- Раунды и рассылка ----------

    private async Task BeginRound()
    {
        _engine.StartRound();

        foreach (var (playerId, side) in _sides)
        {
            var payload = new
            {
                round = State.Round,
                support_cards_drawn = side.Hand.TakeLast(1).ToArray()
            };
            await SendTo(playerId, "round_start", payload);
            await SendTo(playerId, "dice_rolled", new
            {
                you = side.Dice.Dice.Select(d => d.ToString().ToLower()).ToArray(),
                opponent_hidden = true
            });
        }
        await SyncAll();
    }

    private async Task SyncAll()
    {
        foreach (var playerId in _sides.Keys)
            await SendStateSync(playerId);
    }

    private async Task SendStateSync(string playerId)
    {
        var view = _sides[playerId];
        var foe = State.Other(view);
        await SendTo(playerId, "state_sync", new
        {
            round = State.Round,
            phase = State.Phase.ToString().ToLower(),
            you = SideView(view),
            opponent = OpponentView(foe),
            active_character = view.Active.Uid
        });
    }

    private object SideView(PlayerSide s) => new
    {
        player_id = s.PlayerId,
        characters = s.Characters.Select(CharacterView),
        hand = s.Hand.ToArray(),
        dice = s.Dice.Dice.Select(d => d.ToString().ToLower()).ToArray(),
        supports_on_field = s.FieldSupports.ToArray(),
        rerolls_left = s.RerollsLeft
    };

    private object OpponentView(PlayerSide s) => new
    {
        player_id = s.PlayerId,
        characters = s.Characters.Select(CharacterView),
        hand_count = s.Hand.Count,
        supports_on_field = s.FieldSupports.ToArray()
    };

    private static object CharacterView(CharacterState c) => new
    {
        uid = c.Uid,
        def_id = c.DefId,
        hp = c.Hp,
        max_hp = c.MaxHp,
        energy = c.Energy,
        energy_max = c.EnergyMax,
        element = c.Element.ToString().ToLower(),
        shield = c.Shield,
        alive = c.Alive
    };

    /// <summary>Последняя строка лога движка как action_result (v0-упрощение).</summary>
    private async Task SendLastLogAsync(string actor, string toPlayerId)
    {
        if (_conns.TryGetValue(toPlayerId, out var conn))
            await conn.SendAsync("action_result", new
            {
                actor,
                log = State.Log.Count > 0 ? State.Log[^1] : ""
            });
    }

    private async Task FinishGame()
    {
        if (_gameOverSent) return;
        _gameOverSent = true;

        string winner = State.WinnerId!;
        foreach (var playerId in _sides.Keys)
        {
            if (playerId == _botPlayerId) continue; // боту аккаунта нет — награды не начисляем

            bool win = playerId == winner;
            _gacha.AddBattleRewards(playerId, win);
            await SendTo(playerId, "game_over", new
            {
                winner = win ? "you" : "opponent",
                round = State.Round,
                rewards = new { dust = win ? GachaConfig.RewardDustWin : GachaConfig.RewardDustLose, currency = 0 }
            });
        }
    }

    // ---------- Утилиты ----------

    private BattleState State => _engine.State;

    private PlayerSide? SideOf(ClientConnection conn) =>
        conn.PlayerId != null && _sides.TryGetValue(conn.PlayerId, out var s) ? s : null;

    private CharacterState? FindChar(string uid) =>
        _sides.Values.SelectMany(s => s.Characters).FirstOrDefault(c => c.Uid == uid);

    private static CharacterState FindOwn(PlayerSide side, string uid) =>
        side.Characters.First(c => c.Uid == uid);

    private async Task SendTo(string playerId, string type, object payload)
    {
        if (_conns.TryGetValue(playerId, out var conn))
            await conn.SendAsync(type, payload);
    }
}
