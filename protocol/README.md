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
This first foundation defines the message envelope, core control messages, identifiers, and compatibility rules. It does not yet implement Bluetooth or cryptography.

See `messages.md` and `versioning.md`.
