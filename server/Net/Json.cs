using System.Text.Json;

namespace Server.Net;

/// <summary>JSON-конверт протокола: {type, payload}, snake_case (protocol-v0.md).</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public static string Envelope(string type, object? payload) =>
        JsonSerializer.Serialize(new { type, payload }, Options);

    public static (string Type, JsonElement Payload)? Parse(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var t)) return null;
            var payload = root.TryGetProperty("payload", out var p) ? p.Clone() : default;
            return (t.GetString()!, payload);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Достать строковое поле из payload.</summary>
    public static string? Str(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    public static int Int(this JsonElement e, string name, int fallback = 0) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : fallback;

    public static int[] IntArray(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(x => x.GetInt32()).ToArray() : Array.Empty<int>();

    /// <summary>Достать массив из payload; если поля нет — пустой массив.</summary>
    public static JsonElement Arr(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v : JsonSerializer.SerializeToElement(Array.Empty<object>());

    public static bool Bool(this JsonElement e, string name, bool fallback = false) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
            ? v.GetBoolean() : fallback;
}
