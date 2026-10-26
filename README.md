# ClipBridge

ClipBridge privately moves copied text between one Windows laptop and one Android phone on the same local network.

## What works

- Copy text on Windows; it is encrypted, sent to Android, placed on the phone clipboard, and accompanied by a **Copy** notification fallback.
- On Android, select text in any app, tap **Share**, and choose **Send to PC**; it becomes the Windows clipboard.
- Pair without an account by scanning the QR code shown by the Windows app.
- Pause syncing, rotate the Windows pairing code, or forget the PC from Android.
- Text is limited to 256 KiB, never stored as clipboard history, authenticated with AES-256-GCM, replay-protected, and accepted only for five minutes.

## Install the development build

### Windows

1. Open [`dist/windows-x64`](dist/windows-x64).
2. Run `ClipBridge.exe`.
3. If Windows Firewall asks, allow access on **Private networks** only.
4. Leave ClipBridge running in the notification area.

The development executable is self-contained and does not require a separate .NET installation. It is not code-signed, so Windows may show a SmartScreen warning. Public distribution requires an owner-controlled signing certificate.

### Android

1. Transfer [`dist/ClipBridge-Android-debug.apk`](dist/ClipBridge-Android-debug.apk) to the phone.
2. Allow installation from the app used to open the APK, then install it.
3. Open ClipBridge, grant camera and notification access, and scan the QR code on Windows.
4. Keep both devices on the same Wi-Fi network. VPNs and guest networks can block local-device connections.

The APK is signed with Android's development key and is intended for local testing. A Play Store release requires a private release key, store listing, privacy disclosure, and current policy review.

## Use it

**Windows to Android:** copy ordinary text on Windows. Android receives it automatically. If Android has cleared or rejected a background clipboard write, tap **Copy** on the ClipBridge notification.

**Android to Windows:** select text, tap **Share**, then **Send to PC**. The Windows clipboard is updated when the devices connect.

Use **Pair a new phone** on Windows to revoke the old pairing secret. Use **Forget this PC** on Android before pairing with a different computer.

## Security boundaries

- Clipboard content is encrypted end-to-end at the application layer; the local WebSocket itself is cleartext, so IP addresses, timing, and message sizes remain observable on the LAN.
- Anyone who scans or photographs the pairing QR code while it is visible obtains the pairing secret. Rotate pairing afterward if the code may have been exposed.
- Clipboard managers inherently process sensitive data. Pause ClipBridge before copying passwords, recovery phrases, payment details, or other secrets.
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
