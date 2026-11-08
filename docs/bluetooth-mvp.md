# Bluetooth MVP

This slice connects the Android RFCOMM server to the Windows client without a server.

## Test flow

1. Install/run the Android app.
2. Tap **Make this device discoverable** and allow the requested 5-minute discoverability window.
3. On Windows, run the client. It searches paired/visible Bluetooth devices for the Ecosys RFCOMM service UUID.
4. When connected, Windows sends an Ecosys v1 `hello` message.
5. Android shows the received message type.

The transport currently uses newline-delimited UTF-8 JSON for small control messages.

## Clipboard sharing

Section **ZWISCHENABLAGE** in both apps. Sharing is off by default and per connection.

1. Connect Windows and Android.
2. Confirm the peer first (see **Gerätebestätigung und Verlauf** below), then tap
   **Teilen aktivieren**. The app announces `clipboard.state` and sends the current text once.
3. Copy text on either device — it appears in the other device's clipboard.
4. **Jetzt senden** pushes the current clipboard even when the other side already has that value.

Limits and behaviour:

- at most 65 536 characters per value; longer or empty text is not sent
- identical values are not re-sent, and a value that arrived from the peer is never echoed back
- the peer validates `hash` and `bytes`, so a corrupted or edited message is dropped
- disabling sharing stops both directions; a disconnect disables it as well

## Gerätebestätigung und Verlauf

Section **BEKANNTE GERÄTE** in both apps. Every `hello` adds the peer to a local history; the entry
shows platform, when it was last seen and whether it is confirmed.

1. Connect and send `hello` once so the neighbour is known.
2. Tap **Vertrauen** on the entry. On Windows the same confirmation is offered when clipboard
   sharing is enabled for a device that is not confirmed yet.
3. **Bestätigung entziehen** revokes it; sharing stops immediately if it was active.
4. **Verlauf leeren** removes unconfirmed entries only. Confirmed devices stay, because deleting
   them would silently revoke a decision the user made.

Storage: Android keeps the list in its private `SharedPreferences`, Windows in
`%APPDATA%\Ecosys\peers.json`. Both clients keep at most 20 unconfirmed entries and never prune a
confirmed device. Confirmation is bound to the identity ID from the envelope, so it survives a
renamed Bluetooth device, but it is not a cryptographic proof — that is what the session handshake
still has to provide.

## Current limitation

Windows is the client and Android is the RFCOMM server in this MVP. Cryptographic session
establishment is intentionally the next security slice — until then the clipboard gate above
records a user decision, not protection: the traffic is plain text on the Bluetooth link.
