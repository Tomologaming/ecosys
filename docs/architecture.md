# Ecosys Architecture

Ecosys is designed as a serverless, device-to-device ecosystem. Android and Windows are the first targets.

## Layers

### Core
Shared concepts that should remain platform independent:
- device identity
- protocol messages
- capability negotiation
- session state
- pairing/trust model
- future encryption/session management

### Transport
Platform-specific connectivity:
- Bluetooth — first implementation
- Wi-Fi/LAN — later optional high-throughput transport
- USB — later optional transport

### Platform
Android and Windows provide:
- Bluetooth APIs
- permissions
- lifecycle/background behavior
- user-facing pairing and trust UI
- local storage

## First milestone
1. Android and Windows discover each other over Bluetooth.
2. The user explicitly confirms the peer.
3. A secure session is established.
4. Both sides exchange `hello` / `hello.ack`.
5. A test message travels in both directions.

No cloud service is required for this flow.
