# ClipBridge

ClipBridge privately moves copied text between one Windows laptop and one Android phone on the same local network.

> **Project status:** public-source development release. The protocol and automated tests are available for review, but the project has not received an independent security audit. Repository builds are unsigned development artifacts unless a release explicitly says otherwise.

## What works

- Copy text on Windows; it is encrypted, sent to Android, placed on the phone clipboard, and accompanied by a **Copy** notification fallback.
- On Android, select text in any app, tap **Share**, and choose **Send to PC**; it becomes the Windows clipboard.
- Pair without an account by scanning the short-lived QR invitation shown by the Windows app.
- Reconnect automatically with authenticated LAN discovery when the laptop's local IP address changes.
- Pause syncing, rotate the Windows pairing code, or forget the PC from Android.
- Each connection derives fresh directional AES-256-GCM keys with authenticated ephemeral ECDH; text is limited to 256 KiB and never stored as clipboard history.

## Install the development build

Build artifacts are intentionally not committed to the repository. Build them locally using the instructions below. Only install binaries from a source and commit you trust.

### Windows

1. Build the project, then open `dist/windows-x64`.
2. Run `ClipBridge.exe`.
3. If Windows Firewall asks, allow access on **Private networks** only.
4. Leave ClipBridge running in the notification area.

The development executable is self-contained and does not require a separate .NET installation. For a searchable Start Menu entry and automatic startup, run `dist\installer\Install-ClipBridge.ps1`. The build is not code-signed, so Windows may show a SmartScreen warning. Public distribution requires an owner-controlled signing certificate.

### Android

1. Build the project, then transfer `dist/ClipBridge-Android-debug.apk` to the phone.
2. Allow installation from the app used to open the APK, then install it.
3. Open ClipBridge, grant camera and notification access, and scan the QR code on Windows.
4. Keep both devices on the same Wi-Fi network. VPNs and guest networks can block local-device connections.

The APK is signed with Android's development key and is intended for local testing. A Play Store release requires a private release key, store listing, privacy disclosure, and current policy review.

## Use it

**Windows to Android:** copy ordinary text on Windows. Android receives it automatically. If Android has cleared or rejected a background clipboard write, tap **Copy** on the ClipBridge notification.

**Android to Windows:** select text, tap **Share**, then **Send to PC**. The Windows clipboard is updated when the devices connect.

Use **Pair a new phone** on Windows to revoke the old pairing secret. Use **Forget this PC** on Android before pairing with a different computer.

## Privacy modes

- **Smart Sync** is the default. Likely secrets and copies from common password managers are blocked locally with generic feedback.
- **Private Mode** pauses transfers for ten minutes and resumes automatically.
- **Manual mode** disables automatic Windows clipboard sharing. Press the configurable global shortcut (default **Ctrl+Alt+S**) or click **Send clipboard now** to deliberately send the current clipboard.

Sensitive detection is defense in depth, not a replacement for a password manager. Read [`PRIVACY.md`](PRIVACY.md) and [`SECURITY.md`](SECURITY.md) before distributing ClipBridge.

## Security boundaries

- Clipboard content is encrypted end-to-end at the application layer with a fresh key per connection; IP addresses, timing, and approximate message sizes remain observable on the LAN.
- Treat a visible pairing QR as private and rotate pairing if it may have been photographed.
- Clipboard managers inherently process sensitive data. Smart Sync reduces accidental sharing but cannot identify every secret.
- ClipBridge has no cloud service and cannot sync across unrelated networks.

## Build from source

Requirements:

- Windows 10 1809 or newer
- .NET 10 SDK
- JDK 17
- Android SDK Platform 35 and Build Tools 35

Set `ANDROID_HOME`, then run:

```powershell
./scripts/build.ps1
```

The script runs the complete test suite and creates the Windows self-contained output and Android debug APK in `dist/`.

## Repository layout

- `src/ClipBridge.Protocol`: versioned encryption, envelopes, pairing URI, bounds, and replay protection.
- `src/ClipBridge.Windows`: Windows clipboard host, tray UI, pairing QR, and WebSocket server.
- `android/app`: Android pairing UI, Share Sheet target, foreground sync service, clipboard delivery, and notifications.
- `tests`: .NET protocol and Windows behavior tests.
- [`DECISIONS.md`](DECISIONS.md): product and engineering decisions, including deliberately deferred scope.
- `docs/superpowers`: approved design and implementation plan.

## Contributing and security

Contributions are welcome under the guidelines in [`CONTRIBUTING.md`](CONTRIBUTING.md). Report vulnerabilities privately using [`SECURITY.md`](SECURITY.md); never place clipboard contents, pairing invitations, credentials, or personal information in a public issue.

ClipBridge is released under the [MIT License](LICENSE).
