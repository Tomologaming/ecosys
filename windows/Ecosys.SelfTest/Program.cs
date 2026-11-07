using System.Text.Json;
using Ecosys.Windows.Protocol;
using Ecosys.Windows.Sync;

// Behavioural checks for the pieces of the Windows client that do not need a
// Bluetooth peer: clipboard rules, envelope shape and payload parsing.
// Exits non-zero when a check fails so CI and local runs can gate on it.

var failures = 0;

void Check(string name, bool condition)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")}  {name}");
    if (!condition)
        failures++;
}

void CheckEqual<T>(string name, T expected, T actual)
{
    var ok = EqualityComparer<T>.Default.Equals(expected, actual);
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}");
    if (!ok)
    {
        Console.WriteLine($"      expected: {expected}");
        Console.WriteLine($"      actual:   {actual}");
        failures++;
    }
}

// ---------------------------------------------------------------- hashing
// The same value the Android unit test asserts, so both clients agree.
CheckEqual("hash of \"hallo\" matches the shared vector", "sha256:d3751d33f9cd5049", ClipboardSync.HashOf("hallo"));
CheckEqual("byte length counts UTF-8, not characters", 5, ClipboardSync.Create("grün")!.Bytes);

// ------------------------------------------------------------- validation
Check("null text is rejected", ClipboardSync.Create(null) is null);
Check("empty text is rejected", ClipboardSync.Create("") is null);
Check("text over the limit is rejected", ClipboardSync.Create(new string('a', ClipboardSync.MaxChars + 1)) is null);
Check("text at the limit is accepted", ClipboardSync.Create(new string('a', ClipboardSync.MaxChars)) is not null);

var hallo = ClipboardSync.Create("hallo")!;
Check("valid payload is accepted", ClipboardSync.Accept("hallo", hallo.Hash, hallo.Bytes) == hallo);
Check("mismatching hash is rejected", ClipboardSync.Accept("hallo", "sha256:0000000000000000", hallo.Bytes) is null);
Check("mismatching byte count is rejected", ClipboardSync.Accept("hallo", hallo.Hash, 99) is null);
Check("missing hash is rejected", ClipboardSync.Accept("hallo", null, hallo.Bytes) is null);
Check("missing byte count is tolerated", ClipboardSync.Accept("hallo", hallo.Hash) == hallo);

// ---------------------------------------------------------------- gating
var sync = new ClipboardSync();
Check("sharing is off by default", !sync.Enabled);
Check("disabled sharing sends nothing", sync.Outgoing("hallo") is null);
Check("disabled sharing accepts nothing", sync.Incoming("hallo", hallo.Hash) is null);

Check("enabling reports a change", sync.SetEnabled(true));
Check("enabling twice reports no change", !sync.SetEnabled(true));

CheckEqual("first distinct value is sent", "hallo", sync.Outgoing("hallo")?.Text);
Check("unchanged value is not sent again", sync.Outgoing("hallo") is null);
Check("empty value is not sent", sync.Outgoing("") is null);
CheckEqual("a new value is sent", "welt", sync.Outgoing("welt")?.Text);

// -------------------------------------------------------- echo prevention
var peer = ClipboardSync.Create("von android")!;
var applied = sync.Incoming(peer.Text, peer.Hash, peer.Bytes);
CheckEqual("incoming value is applied", "von android", applied?.Text);
Check("applied value is not echoed back to the peer", sync.Outgoing("von android") is null);
CheckEqual("local value still goes out afterwards", "lokal", sync.Outgoing("lokal")?.Text);
Check("invalid incoming hash is dropped", sync.Incoming("fremd", "sha256:1111111111111111") is null);

// --------------------------------------------------------------- toggling
sync.SetEnabled(false);
Check("disabling stops sending", sync.Outgoing("hallo") is null);
Check("disabling stops receiving", sync.Incoming(peer.Text, peer.Hash, peer.Bytes) is null);
sync.SetEnabled(true);
CheckEqual("re-enabling forgets the send cache", "hallo", sync.Outgoing("hallo")?.Text);
sync.AllowResend();
CheckEqual("explicit resend pushes the same value again", "hallo", sync.Outgoing("hallo")?.Text);

// ------------------------------------------------------------- envelopes
var message = EcosysMessage.ClipboardSet("windows-abc", hallo);
var json = message.ToJsonString();

using (var document = JsonDocument.Parse(json))
{
    var root = document.RootElement;
    CheckEqual("envelope protocol", "ecosys/1", root.GetProperty("protocol").GetString());
    CheckEqual("envelope type", "clipboard.set", root.GetProperty("type").GetString());
    CheckEqual("envelope sender", "windows-abc", root.GetProperty("sender").GetString());
    Check("envelope carries a message id", root.GetProperty("messageId").GetString() is { Length: > 0 });
    Check("envelope carries a timestamp", root.GetProperty("timestamp").GetInt64() > 0);
    var payload = root.GetProperty("payload");
    CheckEqual("payload format", "text/plain", payload.GetProperty("format").GetString());
    CheckEqual("payload bytes", hallo.Bytes, payload.GetProperty("bytes").GetInt32());
}

Check("clipboard.set round-trips through the parser", EcosysMessage.TryParseClipboard(json, out var parsedText, out var parsedHash, out var parsedBytes));
CheckEqual("round-tripped text", "hallo", parsedText);
CheckEqual("round-tripped hash", hallo.Hash, parsedHash);
CheckEqual("round-tripped byte count", hallo.Bytes, parsedBytes);
Check("clipboard.set is not accepted as clipboard.state", !EcosysMessage.TryParseClipboardState(json, out _, out _));
Check("malformed JSON is rejected", !EcosysMessage.TryParseClipboard("{nope", out _, out _, out _));
Check("foreign protocol JSON is rejected", !EcosysMessage.TryParseClipboard("{\"protocol\":\"other/1\",\"type\":\"clipboard.set\"}", out _, out _, out _));

var stateJson = EcosysMessage.ClipboardState("windows-abc", true).ToJsonString();
Check("clipboard.state round-trips", EcosysMessage.TryParseClipboardState(stateJson, out var stateEnabled, out var stateMax));
Check("clipboard.state enabled flag", stateEnabled);
CheckEqual("clipboard.state advertises the limit", ClipboardSync.MaxChars, stateMax);
Check("clipboard.state is not accepted as clipboard.set", !EcosysMessage.TryParseClipboard(stateJson, out _, out _, out _));

Console.WriteLine();
Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures == 0 ? 0 : 1;
