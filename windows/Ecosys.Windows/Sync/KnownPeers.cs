using System.Globalization;
using System.Text.Json;

namespace Ecosys.Windows.Sync;

/// <summary>A device this client has exchanged messages with before.</summary>
public sealed record KnownPeer(string Id, string Name, string Platform, long LastSeen, bool Trusted);

/// <summary>
/// Local history of peers plus the user's confirmation for each of them.
/// Free of UI types so the rules — cap, ordering, sanitising, tolerant decoding
/// and the trust check the clipboard gate uses — stay testable.
/// </summary>
public sealed class KnownPeers
{
    /// <summary>Upper bound for the stored history. Confirmed devices are never pruned.</summary>
    public const int MaxPeers = 20;

    public const int FormatVersion = 1;

    private const int MaxIdLength = 64;
    private const int MaxNameLength = 64;
    private const int MaxPlatformLength = 16;

    private const long Minute = 60_000L;
    private const long Hour = 60 * Minute;
    private const long Day = 24 * Hour;

    private readonly List<KnownPeer> peers = new();
    private readonly string? path;

    public KnownPeers(string? path = null, string? json = null)
    {
        this.path = path;
        Load(json);
    }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<KnownPeer> List => peers.OrderByDescending(peer => peer.LastSeen).ToList();

    public KnownPeer? Find(string? id) =>
        id is null ? null : peers.FirstOrDefault(peer => peer.Id == id);

    /// <summary>True only for a device the user confirmed; unknown ids are never trusted.</summary>
    public bool IsTrusted(string? id) => Find(id)?.Trusted == true;

    /// <summary>
    /// Records a sighting. Trust survives, the name and the timestamp do not.
    /// Returns true when the stored entry changed and the caller should persist.
    /// </summary>
    public bool Record(string? id, string? name, string? platform, long now)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        var key = id.Trim();
        if (key.Length > MaxIdLength)
            key = key[..MaxIdLength];

        var existing = Find(key);
        var next = new KnownPeer(
            key,
            CleanName(name, existing?.Name),
            CleanPlatform(platform, existing?.Platform),
            Math.Max(0, now),
            existing?.Trusted == true);

        if (next == existing)
            return false;

        if (existing is not null)
            peers.Remove(existing);
        peers.Add(next);
        Prune();
        return true;
    }

    /// <summary>Returns true when the flag changed. Unknown ids are ignored.</summary>
    public bool SetTrusted(string? id, bool trusted)
    {
        var peer = Find(id);
        if (peer is null || peer.Trusted == trusted)
            return false;

        peers.Remove(peer);
        peers.Add(peer with { Trusted = trusted });
        return true;
    }

    public bool Forget(string? id)
    {
        var peer = Find(id);
        if (peer is null)
            return false;

        peers.Remove(peer);
        return true;
    }

    /// <summary>
    /// Clears the history of devices that were never confirmed. Confirmed devices
    /// stay, because dropping them would silently revoke the user's decision.
    /// </summary>
    public int ForgetUntrusted()
    {
        var before = peers.Count;
        peers.RemoveAll(peer => !peer.Trusted);
        return before - peers.Count;
    }

    public void Load(string? json)
    {
        peers.Clear();
        peers.AddRange(Decode(json));
    }

    public string Snapshot() => Encode(List);

    /// <summary>Writes the list to the file given to the constructor. Failures are reported, never thrown.</summary>
    public bool Save()
    {
        if (path is null)
            return false;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, Snapshot());
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Opens the stored list; a missing or damaged file still yields a usable store.</summary>
    public static KnownPeers Open(string? path)
    {
        string? json = null;
        if (path is not null)
        {
            try
            {
                if (File.Exists(path))
                    json = File.ReadAllText(path);
            }
            catch (IOException)
            {
                json = null;
            }
            catch (UnauthorizedAccessException)
            {
                json = null;
            }
        }

        return new KnownPeers(path, json);
    }

    public static string Encode(IEnumerable<KnownPeer> entries)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", FormatVersion);
            writer.WriteStartArray("peers");
            foreach (var peer in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("id", peer.Id);
                writer.WriteString("name", peer.Name);
                writer.WriteString("platform", peer.Platform);
                writer.WriteNumber("lastSeen", peer.LastSeen);
                writer.WriteBoolean("trusted", peer.Trusted);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Reads a stored list back. Damaged entries are skipped instead of discarding
    /// the whole history, and duplicate ids keep the newest entry.
    /// </summary>
    public static List<KnownPeer> Decode(string? json)
    {
        var result = new List<KnownPeer>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("peers", out var array) ||
                array.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var entry in array.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    continue;

                var id = entry.TryGetProperty("id", out var idElement) &&
                         idElement.ValueKind == JsonValueKind.String
                    ? idElement.GetString()?.Trim()
                    : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                if (id.Length > MaxIdLength)
                    id = id[..MaxIdLength];

                var peer = new KnownPeer(
                    id,
                    CleanName(Text(entry, "name"), null),
                    CleanPlatform(Text(entry, "platform"), null),
                    Math.Max(0, entry.TryGetProperty("lastSeen", out var lastSeen) &&
                                lastSeen.TryGetInt64(out var parsed) ? parsed : 0L),
                    entry.TryGetProperty("trusted", out var trusted) &&
                    trusted.ValueKind == JsonValueKind.True);

                var index = result.FindIndex(candidate => candidate.Id == peer.Id);
                if (index < 0)
                    result.Add(peer);
                else if (result[index].LastSeen <= peer.LastSeen)
                    result[index] = peer;
            }
        }
        catch (JsonException)
        {
            return new List<KnownPeer>();
        }

        // Bounded even when the stored file was edited by hand.
        return result
            .OrderByDescending(peer => peer.LastSeen)
            .Take(MaxPeers * 2)
            .ToList();
    }

    /// <summary>Compact German description of when a peer was last seen.</summary>
    public static string DescribeLastSeen(long now, long lastSeen)
    {
        var diff = now - lastSeen;
        if (diff < Minute)
            return "gerade eben";
        if (diff < Hour)
            return $"vor {diff / Minute} Min";
        if (diff < Day)
            return $"vor {diff / Hour} Std";
        if (diff < 2 * Day)
            return "gestern";
        if (diff < 30 * Day)
            return $"vor {diff / Day} Tagen";

        return DateTimeOffset.FromUnixTimeMilliseconds(lastSeen)
            .ToLocalTime()
            .ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    private void Prune()
    {
        while (peers.Count > MaxPeers)
        {
            var oldest = peers
                .Where(peer => !peer.Trusted)
                .OrderBy(peer => peer.LastSeen)
                .FirstOrDefault();
            if (oldest is null)
                return;
            peers.Remove(oldest);
        }
    }

    private static string? Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static string CleanName(string? raw, string? fallback)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        var value = trimmed.Length == 0 ? fallback?.Trim() ?? string.Empty : trimmed;
        if (value.Length == 0)
            return "Unbekanntes Gerät";
        return value.Length > MaxNameLength ? value[..MaxNameLength] : value;
    }

    private static string CleanPlatform(string? raw, string? fallback)
    {
        var trimmed = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        var value = trimmed.Length == 0 ? fallback?.Trim() ?? string.Empty : trimmed;
        if (value.Length == 0)
            return "unbekannt";
        return value.Length > MaxPlatformLength ? value[..MaxPlatformLength] : value;
    }
}
