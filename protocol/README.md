# Ecosys Protocol

The Ecosys protocol is transport-independent. Bluetooth is the first transport; Wi-Fi/LAN and USB can be added later without changing application messages.

## Goals
- device-to-device communication without a mandatory cloud/server
- authenticated device identity
- encrypted sessions
- explicit protocol versioning
- small control messages and separate metadata for large payloads
- compatibility across Android, Windows, iOS/iPadOS and macOS

## Current scope
The envelope, control messages, file transfer messages and the ECS1 BLE framing are defined.
Windows, iOS and the web bridge implement the envelope and framing today:

| Transport | Platform | Implemented |
|-----------|----------|-------------|
| BLE GATT (service `6e6f4d4f-0002-…`) | Windows server + client, iOS server + client, web client | hello, file.*, ECS1 framing |
| RFCOMM (service `6e6f4d4f-0001-…`) | Android server + client, Windows client | newline-delimited JSON: hello, clipboard.* |

Pairing and the encrypted session described in `security.md` are designed but not implemented
yet; application data therefore stays limited to the hello test, user-initiated file transfers
and opt-in clipboard sharing between devices the user explicitly connected.

See `messages.md`, `security.md` and `versioning.md`.
