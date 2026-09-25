# Windows Installer

The Safe installer is deliberately manual.

1. Run **Promote Main to Safe** and enter PROMOTE.
2. Run **Safe Windows Release** and enter RELEASE.
3. The release contains the online installer, offline installer, ZIP payload and SHA-256 checksum.

The online installer downloads the newest published Safe payload. The offline installer contains the exact payload from that release.

SmartScreen cannot be disabled by the application; public distribution should use a trusted code-signing identity.
