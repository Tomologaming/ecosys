# Ecosys

A direct connection between Windows, Android, iOS/iPadOS — and later macOS. Devices find each
other over Bluetooth and talk to each other; no cloud, no account, no relay server.

Current status: Windows, Android and iOS apps exchange `hello` messages, the Windows and iOS
apps plus the web bridge speak the Ecosys BLE file-transfer messages, and Windows ↔ Android can
share plain-text clipboard content — off by default, only after the connection was confirmed.
Pairing and encrypted sessions are designed (see [protocol/security.md](protocol/security.md))
but not implemented yet, so clipboard traffic is unencrypted and today's build is a development
preview.

| Part | Path | State |
|------|------|-------|
| Windows client (.NET 8, WinForms) | [windows/](windows) | builds, runs, ships with installer |
| Android app (Kotlin, RFCOMM) | [android/](android) | builds, runs on device/emulator |
| iOS app (SwiftUI, BLE) | [ios/](ios) | builds unsigned IPA via CI |
| Web bridge (static, Web Bluetooth) | [website/](website) | file transfer through ECS1 framing |
| Protocol specification | [protocol/](protocol) | envelope, messages, framing, security |

## Build

```powershell
# Windows client
dotnet build windows/Ecosys.Windows/Ecosys.Windows.csproj -c Release
dotnet run --project windows/Ecosys.SelfTest/Ecosys.SelfTest.csproj -c Release
dotnet publish windows/Ecosys.Windows/Ecosys.Windows.csproj -c Release -r win-x64 --self-contained true -o dist/publish

# Installer (Inno Setup 7)
& "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe" /DOfflineMode /FEcosys-Offline-Setup installer/Ecosys.iss
& "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe" installer/Ecosys.iss
```

```bash
# Android (needs JDK 17 and the Android SDK 35)
cd android && gradle assembleDebug
cd android && gradle testDebugUnitTest
```

The iOS app is built by the `Build iOS` workflow on a macOS runner; local builds need Xcode 15+.

## Documentation

- [docs/architecture.md](docs/architecture.md) — layers and the first milestone
- [docs/bluetooth-mvp.md](docs/bluetooth-mvp.md) — what runs on which platform and how to test it
- [docs/windows-installer.md](docs/windows-installer.md) — installer layout, checksums, uninstall
- [docs/roadmap.md](docs/roadmap.md) — phase status
- [protocol/messages.md](protocol/messages.md) — envelope, messages, BLE framing

Current release action:
[![Safe Windows Release](https://github.com/Tomologaming/ecosys/actions/workflows/release-safe.yml/badge.svg?branch=main)](https://github.com/Tomologaming/ecosys/actions/workflows/release-safe.yml)
