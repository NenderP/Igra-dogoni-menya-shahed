namespace Server.Net;

/// <summary>
/// Матчмейкинг v0: лобби-коды для друзей + пул случайных соперников.
/// Пару отдаёт наружу через событие OnPaired — Program создаёт GameSession.
/// </summary>
public class Matchmaker
{
    private readonly Queue<ClientConnection> _randomQueue = new();
    private readonly Dictionary<string, ClientConnection> _lobbyHosts = new();
    private readonly object _lock = new();

    /// <summary>(connA, connB) — пара готова к бою.</summary>
    public event Action<ClientConnection, ClientConnection>? OnPaired;

    public string CreateLobby(ClientConnection host)
    {
        lock (_lock)
        {
            string code;
            do { code = Guid.NewGuid().ToString("N")[..4].ToUpper(); }
            while (_lobbyHosts.ContainsKey(code));
            _lobbyHosts[code] = host;
            return code;
        }
    }

    public bool TryJoinLobby(ClientConnection guest, string code)
    {
        ClientConnection? host;
        lock (_lock)
        {
            if (!_lobbyHosts.TryGetValue(code.ToUpper(), out host)) return false;
            _lobbyHosts.Remove(code.ToUpper());
        }
        OnPaired?.Invoke(host, guest);
        return true;
    }

    public void EnqueueRandom(ClientConnection conn)
    {
        ClientConnection? other = null;
        lock (_lock)
        {
            if (_randomQueue.Count > 0) other = _randomQueue.Dequeue();
            else _randomQueue.Enqueue(conn);
        }
        if (other != null) OnPaired?.Invoke(other, conn);
    }
}
