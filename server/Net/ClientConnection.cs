using System.Net.WebSockets;
using System.Text;

namespace Server.Net;

/// <summary>Обёртка над WebSocket одного клиента: отправка конвертов, приём строк.</summary>
public class ClientConnection
{
    private readonly WebSocket _ws;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string? PlayerId { get; set; }
    public bool Connected => _ws.State == WebSocketState.Open;

    public ClientConnection(WebSocket ws) => _ws = ws;

    public async Task SendAsync(string type, object? payload)
    {
        if (!Connected) return;
        var bytes = Encoding.UTF8.GetBytes(Json.Envelope(type, payload));
        await _sendLock.WaitAsync();
        try
        {
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Читает одно сообщение целиком. null = клиент отключился.</summary>
    public async Task<string?> ReceiveAsync()
    {
        var buffer = new byte[64 * 1024];
        var sb = new StringBuilder();
        while (true)
        {
            WebSocketReceiveResult res;
            try
            {
                res = await _ws.ReceiveAsync(buffer, CancellationToken.None);
            }
            catch
            {
                return null;
            }
            if (res.MessageType == WebSocketMessageType.Close) return null;
            sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count));
            if (res.EndOfMessage) return sb.ToString();
        }
    }
}
