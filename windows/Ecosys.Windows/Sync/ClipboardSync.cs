using System.Security.Cryptography;
using System.Text;

namespace Ecosys.Windows.Sync;

/// <summary>A clipboard value that passed validation, with the fields a clipboard.set carries.</summary>
public sealed record ClipboardUpdate(string Text, string Hash, int Bytes);

/// <summary>
/// Clipboard synchronisation rules. Free of UI dependencies so limits, hashing,
/// echo suppression and gating can be exercised by Ecosys.SelfTest.
/// </summary>
public sealed class ClipboardSync
{
    /// <summary>Largest text a single clipboard.set may carry.</summary>
    public const int MaxChars = 65_536;

    public const string Format = "text/plain";

    private string? lastSentHash;
    private string? lastAppliedHash;

    public bool Enabled { get; private set; }

    /// <summary>sha256: plus the first 16 hex characters of the UTF-8 SHA-256 digest.</summary>
    public static string HashOf(string text)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return "sha256:" + Convert.ToHexString(digest)[..16].ToLowerInvariant();
    }

    /// <summary>Builds an update, or null when the text is empty or too large to send.</summary>
    public static ClipboardUpdate? Create(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        if (text.Length > MaxChars)
            return null;
        return new ClipboardUpdate(text, HashOf(text), Encoding.UTF8.GetByteCount(text));
    }

    /// <summary>
    /// Validates an incoming payload. Returns null when the content is empty, oversized,
    /// or does not match the declared hash or byte length. A missing byte count (-1)
    /// skips the length check; the hash is always required.
    /// </summary>
    public static ClipboardUpdate? Accept(string? text, string? hash, int bytes = -1)
    {
        if (string.IsNullOrEmpty(hash))
            return null;
        var update = Create(text);
        if (update is null)
            return null;
        if (hash != update.Hash)
            return null;
        if (bytes >= 0 && bytes != update.Bytes)
            return null;
        return update;
    }

    /// <summary>Returns true when the state actually changed.</summary>
    public bool SetEnabled(bool enabled)
    {
        if (Enabled == enabled)
            return false;
        Enabled = enabled;
        lastSentHash = null;
        return true;
    }

    /// <summary>Lets the explicit "send now" action push an unchanged value a second time.</summary>
    public void AllowResend() => lastSentHash = null;

    /// <summary>
    /// The update to send for the current local clipboard, or null when sharing is off,
    /// the text is unusable, or the value is unchanged or an echo of a remote update.
    /// </summary>
    public ClipboardUpdate? Outgoing(string? text)
    {
        if (!Enabled)
            return null;
        var update = Create(text);
        if (update is null)
            return null;
        if (update.Hash == lastSentHash)
            return null;
        if (update.Hash == lastAppliedHash)
            return null;
        lastSentHash = update.Hash;
        return update;
    }

    /// <summary>
    /// Accepts an incoming clipboard.set. Returns the update to put on the local
    /// clipboard, or null when sharing is off or the payload is invalid.
    /// An accepted value is remembered as both applied and sent so the resulting
    /// clipboard change is not echoed back to the peer.
    /// </summary>
    public ClipboardUpdate? Incoming(string? text, string? hash, int bytes = -1)
    {
        if (!Enabled)
            return null;
        var update = Accept(text, hash, bytes);
        if (update is null)
            return null;
        lastAppliedHash = update.Hash;
        lastSentHash = update.Hash;
        return update;
    }
}
