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
2. Tap **Teilen aktivieren** on either side and confirm the warning that the link is not
   encrypted yet. The app announces `clipboard.state` and sends the current text once.
3. Copy text on either device — it appears in the other device's clipboard.
4. **Jetzt senden** pushes the current clipboard even when the other side already has that value.

Limits and behaviour:

- at most 65 536 characters per value; longer or empty text is not sent
- identical values are not re-sent, and a value that arrived from the peer is never echoed back
- the peer validates `hash` and `bytes`, so a corrupted or edited message is dropped
- disabling sharing stops both directions; a disconnect disables it as well

## Current limitation

Windows is the client and Android is the RFCOMM server in this MVP. Pairing/trust UI and
cryptographic session establishment are intentionally the next security slice — until then the
clipboard gate above is a warning, not protection: the traffic is plain text on the Bluetooth
link.
