# Windows Installer

Ecosys has two Windows installer modes:

- **Ecosys-Setup.exe** — small online installer. It always downloads the newest published build from the GitHub Safe release channel.
- **Ecosys-Offline-Setup.exe** — self-contained installer containing the exact Safe build that was used to create that release.

## Channels

- `main` is the development/testing branch.
- `Safe` is the release branch.
- Every push to `Safe` creates a versioned GitHub Release containing the Windows application archive, checksum, online installer and offline installer.

The online installer uses the stable GitHub Releases download endpoint, so users do not need to know a release tag.

## Why there is no ZIP-in-ZIP installer

The user-facing artifacts are EXE installers. The application ZIP is only a release payload consumed by the installer; it is not the installer users need to launch.

## Security

The Safe release pipeline is intentionally separated from `main`. Release artifacts are produced only from `Safe`.

The release also publishes a SHA-256 checksum for the application archive. Future production hardening should add public code signing and signature verification to the release pipeline.

SmartScreen cannot be disabled by an application. For public distribution, Ecosys should use a stable signing identity and either Microsoft Store signing or a publicly trusted code-signing service. Microsoft documents that new non-Store binaries can still show SmartScreen warnings until publisher/file reputation is established.

## Future auto-update

Running Ecosys-Setup.exe again will always install the newest Safe build. A later phase can add an in-app update agent that checks the same Safe channel and launches the installer silently.
