# Bluetooth MVP

This slice connects the Android RFCOMM server to the Windows client without a server.

## Test flow

1. Install/run the Android app.
2. Tap **Make this device discoverable** and allow the requested 5-minute discoverability window.
3. On Windows, run the client. It searches paired/visible Bluetooth devices for the Ecosys RFCOMM service UUID.
4. When connected, Windows sends an Ecosys v1 `hello` message.
5. Android shows the received message type.

The transport currently uses newline-delimited UTF-8 JSON for small control messages.

## Current limitation

Windows is the client and Android is the RFCOMM server in this MVP. Pairing/trust UI and cryptographic session establishment are intentionally the next security slice.
