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

    public string ToJsonString() => JsonSerializer.Serialize(new {
        protocol = CurrentProtocol,
        type = Type,
        sender = Sender,
        timestamp = Timestamp,
        messageId = MessageId,
        payload = Payload
    });

    public static EcosysMessage Hello(string sender, string deviceName, string platform) =>
        new("hello", sender, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Guid.NewGuid().ToString("N"),
            new Dictionary<string, object?> {
                ["deviceName"] = deviceName,
                ["platform"] = platform
            });
}
