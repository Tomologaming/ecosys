# Bluetooth MVP

This slice connects the native apps and the web bridge without any server.

## What runs where

| Platform | Role | Service |
|----------|------|---------|
| Windows | BLE server (advertises the Ecosys service) and BLE client | `6e6f4d4f-0002-…` |
| Windows | RFCOMM client | `6e6f4d4f-0001-…` |
| iOS | BLE server and BLE client | `6e6f4d4f-0002-…` |
| Android | RFCOMM server and client | `6e6f4d4f-0001-…` |
| Web bridge (`website/index.html`) | BLE client through Web Bluetooth | `6e6f4d4f-0002-…` |

The BLE service exposes a write characteristic `…-0003-…` (peer to app) and a notify
characteristic `…-0004-…` (app to peer).

## Test flow — Windows ↔ Android (RFCOMM)

1. Start the Android app and tap **Dieses Gerät sichtbar machen**; allow the 5-minute window.
2. Start the Windows client, tap **Suchen** and pick the phone in the device list.
3. Windows sends an Ecosys `hello` envelope; Android shows the received message type in the
   activity log.
4. Tap **Hello senden** on Android to send a `hello` back.

## Test flow — Windows ↔ iPhone (BLE)

1. Start Ecosys on both sides. Windows advertises `Ecosys` with the BLE service.
2. On the iPhone tap **Geräte suchen**, then **Verbinden** next to `Ecosys`.
3. Tap **Hello senden** on either side; the other side shows the message.

## Test flow — browser (BLE)

1. Serve `website/` over HTTPS (or `localhost`) and open it in Chromium.
2. **Bluetooth-Gerät suchen** → pick the Ecosys device.
3. Drop files onto the drop zone; the bridge frames them with ECS1 and writes
   `file.start` / `file.chunk` / `file.complete`.

## Current limitation

Pairing/trust UI and cryptographic session establishment are intentionally the next security
slice. Control messages are still plaintext, so the file path is meant for devices the user
physically connected. RFCOMM messages are newline-delimited UTF-8 JSON; BLE messages are plain
JSON when they fit into one attribute write and ECS1-framed otherwise.
