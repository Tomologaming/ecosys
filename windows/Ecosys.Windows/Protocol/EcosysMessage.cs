using System.Text.Json;
using System.Text.Json.Serialization;

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
                ["capabilities"] = new[] { "hello", "ping" }
            });
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
