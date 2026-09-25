namespace Ecosys.Windows.Protocol;

public sealed record EcosysMessage(string Type, string Sender, long Timestamp, string MessageId, Dictionary<string, object?> Payload)
{
    public const string CurrentProtocol = "ecosys/1";
}
