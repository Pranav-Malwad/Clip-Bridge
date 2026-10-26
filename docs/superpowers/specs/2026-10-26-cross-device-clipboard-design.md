# Cross-Device Clipboard Design

## Intent

Build a private, local-first clipboard bridge between one Windows laptop and one Android phone. Copying text on Windows makes it available to paste on Android; sharing selected text to the Android app sends it to the Windows clipboard.

## Goals

- Pair the two devices without creating an account.
- Transfer UTF-8 plain text on the same local network.
- Make Windows-to-Android delivery automatic when Android permits it, with a notification action as the reliable fallback.
- Expose an Android Share target named **Send to PC**.
- Encrypt and authenticate every transferred clipboard item.
- Avoid clipboard echo loops, reject stale/replayed messages, and cap payload size.
- Provide understandable connection, pairing, pause, and error states.

## Non-goals

- Cloud relay or operation across unrelated networks.
- Files, images, rich text, clipboard history, multiple peers, accounts, or remote device management.
- Accessibility-service or custom-keyboard workarounds for Android clipboard restrictions.
- iOS, macOS, or Linux clients.

## Architecture

The Windows app is the local session host. It advertises a `_clipbridge._tcp` service through mDNS, displays a QR pairing payload, listens for authenticated WebSocket connections, watches the Windows clipboard, and writes phone-originated text to it. The Android app discovers the host, scans the QR code, stores the paired identity in Android Keystore-backed preferences, and maintains a foreground connection while syncing is enabled.

Pairing uses an ephemeral code carried by the QR payload. The devices exchange public identity material and derive a per-device shared secret. Application messages use an authenticated envelope containing protocol version, message ID, timestamp, direction, content type, and ciphertext. TLS protects transport; message authentication provides protocol-level identity and replay protection.

The Android share receiver accepts only shared `text/plain` content and sends it to the paired Windows session. Incoming Windows text is written to the Android clipboard when the OS permits; the app always posts a notification with a **Copy** action so delivery remains usable when background clipboard access is restricted.

## Constraints

- Minimum Android version: Android 10 (API 29).
- Windows target: Windows 10 version 1809 or later, x64.
- Maximum clipboard item: 256 KiB encoded as UTF-8.
- Clipboard content is held only in memory and is not persisted as history.
- Pairing secrets are stored with Android Keystore and Windows DPAPI.
- Timestamps outside a five-minute acceptance window and duplicate message IDs are rejected.
- The first release supports one paired Android device per Windows installation.

## Approaches

### Selected: native C#/.NET and Kotlin

This provides direct, supportable access to Windows clipboard/tray APIs and Android sharing, notifications, lifecycle, and secure storage. It produces two codebases but minimizes platform integration risk.

### Considered: Flutter

Flutter would share UI code, but both clipboard directions, Windows tray behavior, Android sharing, discovery, and background lifecycle still require native plugins. That adds bridge complexity around the core feature.

### Considered: Kotlin Multiplatform

It could share protocol code, but Windows UI and clipboard integration are less conventional than .NET and would increase packaging and maintenance cost for a two-platform MVP.

## UX

Windows runs as a small desktop/tray application with device status, pause/resume, pair/re-pair, and last-transfer status. Android has a compact status screen, QR scanner/manual pairing entry, pause/resume, and an Android Share Sheet target. Clipboard contents never appear in the UI unless the user explicitly opens a transient transfer preview.

## Risks and Mitigations

- Android vendor background restrictions: use a foreground service only while sync is enabled and retain the notification Copy fallback.
- Local-network isolation or VPNs: expose a clear disconnected state and manual address pairing fallback.
- Clipboard sensitivity: local-only operation, end-to-end authenticated encryption, no history, size limit, pause control, and future deny-list hooks.
- Self-generated clipboard loops: tag locally applied items and suppress matching clipboard notifications.
- Malicious LAN traffic: pairing-bound identity, authenticated envelopes, replay cache, timestamp validation, and bounded parsing.

## Validation

- Unit-test protocol serialization, size bounds, authentication failure, replay rejection, expiration, and loop suppression.
- Unit-test Windows clipboard coordination with an in-memory clipboard adapter.
- Unit-test Android share-intent extraction and incoming-message decisions.
- Build Windows Debug/Release outputs and Android unit-test/debug APK outputs.
- Perform a manual two-device smoke test when an Android device is available.
