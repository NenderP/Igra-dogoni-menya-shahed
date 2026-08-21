namespace Gacha;

/// <summary>
/// Аккаунт v1 — без пароля (решение по протоколу v0, вопрос №1: player_id = личность).
/// Сервер выдаёт player_id при первом hello, клиент хранит локально.
/// Позже можно апгрейдить до токена/пароля без ломки API.
/// </summary>
public class Account
{
    public string PlayerId { get; }
    public string DisplayName { get; set; }
    public DateTime CreatedAt { get; }
    public PlayerCollection Collection { get; }

    public Account(string playerId, string displayName)
    {
        PlayerId = playerId;
        DisplayName = displayName;
        CreatedAt = DateTime.UtcNow;
        Collection = new PlayerCollection();
    }
}

/// <summary>
/// Сервис аккаунтов — in-memory для v0, позже заменить на БД/файл.
/// </summary>
public class AccountService
{
    private readonly Dictionary<string, Account> _accounts = new();

    public Account GetOrCreate(string playerId, string displayName)
    {
        if (_accounts.TryGetValue(playerId, out var acc)) return acc;
        acc = new Account(playerId, displayName);
        _accounts[playerId] = acc;
        return acc;
    }

    public Account? Get(string playerId) => _accounts.TryGetValue(playerId, out var a) ? a : null;

    public bool Exists(string playerId) => _accounts.ContainsKey(playerId);
}
