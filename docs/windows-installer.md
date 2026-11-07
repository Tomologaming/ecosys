# Windows Installer

The Safe installer is deliberately manual.

1. Run **Promote Main to Safe** and enter PROMOTE.
2. Run **Safe Windows Release** and enter RELEASE.
3. The release contains the online installer, offline installer, ZIP payload and SHA-256 checksum.

The online installer downloads `Ecosys-windows-safe.zip.sha256` first, validates the hash format and then
downloads the payload with that hash, so a corrupted or replaced download is rejected before extraction.
The offline installer contains the exact payload from that release.

Both installers are built with Inno Setup 7 (the release workflow installs `JRSoftware.InnoSetup.7`
and falls back to Chocolatey, then refuses to continue unless `ISCC.exe` reports version 7).
Inno Setup 7 installs per user by default, so `ISCC.exe` is either at
`%LOCALAPPDATA%\Programs\Inno Setup 7\` or under `Program Files`; both paths are searched.

The online installer extracts the ZIP with `ExtractArchive` at runtime, which needs
`ArchiveExtraction=full` in `[Setup]`. Because Inno Setup cannot track files written by Pascal code,
`[UninstallDelete]` removes the whole `{app}` directory, so uninstalling leaves nothing behind.

The installer installs per user into `%LOCALAPPDATA%\Programs\Ecosys` and does not require administrator
rights. SmartScreen cannot be disabled by the application; public distribution should use a trusted
code-signing identity.

## Local build

```powershell
dotnet publish windows/Ecosys.Windows/Ecosys.Windows.csproj -c Release -r win-x64 --self-contained true -o dist/publish

# Inno Setup 7 (per-user install)
& "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe" /DOfflineMode /FEcosys-Offline-Setup installer/Ecosys.iss
& "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe" installer/Ecosys.iss
```

Output lands in `installer/Output`.

## Verified install cycle

The offline installer was installed, started and uninstalled on Windows 11:

1. `Ecosys-Offline-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART` installs to
   `%LOCALAPPDATA%\Programs\Ecosys` and creates the start-menu entry.
2. `Ecosys.Windows.exe` starts and advertises the BLE service.
3. `unins000.exe /VERYSILENT` removes the shortcuts, the uninstall registry key and — through
   `[UninstallDelete]` — the complete application directory.
