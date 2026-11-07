# Ecosys Roadmap

## Phase 1 — Protocol foundation
- [x] Define transport-independent message envelope
- [x] Define initial control messages
- [x] Define protocol compatibility rules
- [x] Document core/transport/platform separation
- [x] Define file transfer messages and ECS1 BLE framing

## Phase 2 — Android foundation
- [x] Kotlin project
- [x] Bluetooth transport abstraction (RFCOMM)
- [x] Android Bluetooth discovery/connection
- [ ] Pairing/trust UI
- [ ] Background connection/event handling

## Phase 3 — Windows foundation
- [x] .NET client
- [x] Bluetooth transport implementation (RFCOMM client, BLE server and client)
- [x] Installer (online and offline)
- [ ] Pairing/trust UI
- [ ] Background/reconnect handling

## Phase 4 — Android ↔ Windows MVP
- [x] Discovery
- [x] hello / hello.ack (hello only)
- [x] Bidirectional test message
- [x] Diagnostics/logging
- [ ] Explicit peer confirmation
- [ ] Secure session

## Phase 5 — Useful features
- [x] File transfer messages (BLE, base64 chunks)
- [ ] File receiver in the Android app
- [x] Clipboard synchronization (Windows ↔ Android, opt-in, not yet encrypted)
- [ ] Connection history
- [ ] Multiple trusted devices

## Phase 6 — More transports
- [ ] Wi-Fi/LAN transport
- [ ] USB transport

## Phase 7 — Apple platforms
- [x] iOS/iPadOS BLE client and server
- [ ] macOS client
- [ ] AltStore distribution where appropriate
