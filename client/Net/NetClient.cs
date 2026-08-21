using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Igra.Client.Net;

/// <summary>Асинхронный WebSocket-клиент: приём крутится в фоне, сообщения складываются в очередь.</summary>
public class NetClient
{
    private ClientWebSocket? _ws;
    private readonly ConcurrentQueue<(string Type, JsonElement Payload)> _inbox = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public bool Connected { get; private set; }
    public static string DefaultServerUrl { get; set; } = "ws://localhost:5050/ws";
    public string ServerUrl { get; set; } = DefaultServerUrl;

    /// <summary>Забрать накопившиеся сообщения (вызывать из Update).</summary>
    public bool TryTake(out (string Type, JsonElement Payload) msg) => _inbox.TryDequeue(out msg);

    public async Task ConnectAsync()
    {
        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri(ServerUrl), CancellationToken.None);
        Connected = true;
        _ = ReceiveLoopAsync();
    }

    public async Task SendAsync(string type, object? payload)
    {
        if (!Connected || _ws == null) return;
        var json = JsonSerializer.Serialize(new { type, payload }, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
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

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[256 * 1024];
        var ws = _ws!;
        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult res;
            try
            {
                res = await ws.ReceiveAsync(buffer, CancellationToken.None);
            }
            catch
            {
                break;
            }
            if (res.MessageType == WebSocketMessageType.Close) break;

            var text = Encoding.UTF8.GetString(buffer, 0, res.Count);
            try
            {
                using var doc = JsonDocument.Parse(text);
                var type = doc.RootElement.GetProperty("type").GetString() ?? "";
                var payload = doc.RootElement.TryGetProperty("payload", out var p)
                    ? p.Clone() : JsonSerializer.SerializeToElement(new { });
                _inbox.Enqueue((type, payload));
            }
            catch
            {
                // битое сообщение — пропускаем
            }
        }
        Connected = false;
    }

    public static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };
}

public static class JsonExt
{
    public static string? Str(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    public static int Int(this JsonElement e, string name, int fallback = 0) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : fallback;

    public static JsonElement Arr(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v : JsonSerializer.SerializeToElement(Array.Empty<object>());
}
