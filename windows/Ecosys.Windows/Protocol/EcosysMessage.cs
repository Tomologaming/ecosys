using System.Text.Json;
using System.Text.Json.Serialization;
using Ecosys.Windows.Sync;

namespace Ecosys.Windows.Protocol;

public sealed record EcosysMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("sender")] string Sender,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("messageId")] string MessageId,
    [property: JsonPropertyName("payload")] Dictionary<string, object?> Payload)
{
    public const string CurrentProtocol = "ecosys/1";

    public string ToJsonString() => JsonSerializer.Serialize(new
    {
        protocol = CurrentProtocol,
        type = Type,
        sender = Sender,
        timestamp = Timestamp,
        messageId = MessageId,
        payload = Payload
    });

    public static EcosysMessage Hello(string sender, string deviceName, string platform) =>
        new("hello", sender, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Guid.NewGuid().ToString("N"),
            new Dictionary<string, object?>
            {
                ["deviceName"] = deviceName,
                ["platform"] = platform,
                ["capabilities"] = new[] { "hello", "ping", "clipboard" }
            });

    public static EcosysMessage ClipboardSet(string sender, ClipboardUpdate update) =>
        new("clipboard.set", sender, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Guid.NewGuid().ToString("N"),
            new Dictionary<string, object?>
            {
                ["text"] = update.Text,
                ["format"] = ClipboardSync.Format,
                ["hash"] = update.Hash,
                ["bytes"] = update.Bytes
            });

    public static EcosysMessage ClipboardState(string sender, bool enabled) =>
        new("clipboard.state", sender, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Guid.NewGuid().ToString("N"),
            new Dictionary<string, object?>
            {
                ["enabled"] = enabled,
                ["maxChars"] = ClipboardSync.MaxChars
            });

    /// <summary>Reads the clipboard.set payload of a received envelope.</summary>
    public static bool TryParseClipboard(string json, out string? text, out string? hash, out int bytes)
    {
        text = null;
        hash = null;
        bytes = -1;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            if (!root.TryGetProperty("protocol", out var protocol) ||
                protocol.ValueKind != JsonValueKind.String ||
                protocol.GetString() != CurrentProtocol)
                return false;
            if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                type.GetString() != "clipboard.set")
                return false;
            if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
                return false;

            if (payload.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                text = textElement.GetString();
            if (payload.TryGetProperty("hash", out var hashElement) && hashElement.ValueKind == JsonValueKind.String)
                hash = hashElement.GetString();
            if (payload.TryGetProperty("bytes", out var bytesElement) && bytesElement.TryGetInt32(out var parsedBytes))
                bytes = parsedBytes;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Reads the clipboard.state payload of a received envelope.</summary>
    public static bool TryParseClipboardState(string json, out bool enabled, out int maxChars)
    {
        enabled = false;
        maxChars = ClipboardSync.MaxChars;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            if (!root.TryGetProperty("protocol", out var protocol) ||
                protocol.ValueKind != JsonValueKind.String ||
                protocol.GetString() != CurrentProtocol)
                return false;
            if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                type.GetString() != "clipboard.state")
                return false;
            if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
                return false;

            if (payload.TryGetProperty("enabled", out var enabledElement))
                enabled = enabledElement.ValueKind == JsonValueKind.True;
            if (payload.TryGetProperty("maxChars", out var maxElement) && maxElement.TryGetInt32(out var parsedMax))
                maxChars = parsedMax;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>Read-only view of a received envelope, used for the activity feed.</summary>
public sealed record ReceivedMessage(string Type, string Sender, string? DeviceName)
{
    public static bool TryParse(string json, out ReceivedMessage? message)
    {
        message = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (!root.TryGetProperty("protocol", out var protocol) ||
                protocol.GetString() != EcosysMessage.CurrentProtocol)
                return false;

            if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
                return false;

            var sender = root.TryGetProperty("sender", out var senderElement) &&
                         senderElement.ValueKind == JsonValueKind.String
                ? senderElement.GetString() ?? string.Empty
                : string.Empty;

            string? deviceName = null;
            if (root.TryGetProperty("payload", out var payload) &&
                payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty("deviceName", out var name) &&
                name.ValueKind == JsonValueKind.String)
            {
                deviceName = name.GetString();
            }

            message = new ReceivedMessage(type.GetString()!, sender, deviceName);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
