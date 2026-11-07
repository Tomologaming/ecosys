# Ecosys Android

Kotlin app with the Ecosys v1 message model, Classic Bluetooth RFCOMM transport (server and
client), Android 12+ Bluetooth permissions, discovery/connection UI and opt-in clipboard sharing
with the Windows client.

```bash
gradle assembleDebug        # APK
gradle testDebugUnitTest    # clipboard rules, JVM only
```

Trust persistence, encryption, background behavior and the file receiver follow in later PRs.
