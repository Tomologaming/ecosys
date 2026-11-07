using System.Text;

namespace Ecosys.Windows.Transport;

/// <summary>Reassembles the ECS1 framing used for larger BLE payloads (web bridge, future file transfer).
/// Plain UTF-8 JSON writes are passed through unchanged so native peers keep working.</summary>
internal sealed class EcosysFrameAssembler
{
    private const int HeaderSize = 10;
    private const int MaxMessageBytes = 4 * 1024 * 1024;

    private ushort currentId;
    private ushort expectedCount;
    private int nextIndex;
    private readonly List<byte[]> parts = new();

    public IReadOnlyList<string> Push(ReadOnlySpan<byte> data)
    {
        var messages = new List<string>();
        if (data.Length == 0)
            return messages;

        if (IsFrame(data))
        {
            if (data.Length < HeaderSize)
                return messages;

            var id = (ushort)(data[4] | (data[5] << 8));
            var index = (ushort)(data[6] | (data[7] << 8));
            var count = (ushort)(data[8] | (data[9] << 8));

            if (count == 0 || index >= count)
                return messages;

            if (index == 0)
            {
                currentId = id;
                expectedCount = count;
                nextIndex = 0;
                parts.Clear();
            }

            if (id != currentId || count != expectedCount || index != nextIndex)
            {
                parts.Clear();
                nextIndex = 0;
                return messages;
            }

            parts.Add(data[HeaderSize..].ToArray());
            nextIndex++;

            if (nextIndex != expectedCount)
                return messages;

            var total = parts.Sum(part => part.Length);
            if (total > MaxMessageBytes)
            {
                parts.Clear();
                return messages;
            }

            var joined = new byte[total];
            var offset = 0;
            foreach (var part in parts)
            {
                part.CopyTo(joined, offset);
                offset += part.Length;
            }

            parts.Clear();
            nextIndex = 0;

            var text = Encoding.UTF8.GetString(joined).TrimEnd('\0', '\r', '\n');
            if (text.Length > 0)
                messages.Add(text);

            return messages;
        }

        var plain = Encoding.UTF8.GetString(data).TrimEnd('\0', '\r', '\n');
        if (plain.Length > 0)
            messages.Add(plain);
        return messages;
    }

    private static bool IsFrame(ReadOnlySpan<byte> data) =>
        data.Length >= 4 &&
        data[0] == 0x45 && // E
        data[1] == 0x43 && // C
        data[2] == 0x53 && // S
        data[3] == 0x31;   // 1

    public void Reset()
    {
        parts.Clear();
        nextIndex = 0;
    }
}
