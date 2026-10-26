# Engineering Decisions

This file records product and engineering decisions made for the initial ClipBridge release.

## Product Scope

- The product is named **ClipBridge**.
- Version 1 transfers plain text only.
- It works on the same local network; there is no cloud relay or account system.
- It supports one Windows laptop paired with one Android phone.
- Windows-to-Android transfer is automatic when Android permits clipboard writes and always includes a notification **Copy** fallback.
- Android-to-Windows uses Android's standard Share Sheet under **Send to PC**.
- Clipboard history is deliberately excluded and content is not persisted.

## Platform Choices

- Windows is implemented in C# on .NET using native Windows clipboard integration.
- Android is implemented in Kotlin with Jetpack Compose.
- Minimum Android version is API 29 (Android 10).
- Minimum Windows version is Windows 10 1809 on x64.
- Flutter and Kotlin Multiplatform were rejected because the native integration would still require platform-specific bridges and add complexity.

## Networking and Pairing

- The Windows app hosts the local session; Android connects to it.
- QR pairing carries the Windows LAN address and is authoritative; manual pairing accepts the same URI. mDNS was omitted from the single-host MVP because it adds multicast/firewall failure modes without improving a completed QR pairing.
- Pairing is account-free and initiated by scanning a QR code displayed by Windows.
- Transport uses a LAN WebSocket carrying AES-256-GCM authenticated application frames. Application encryption avoids unsafe trust bypasses for a locally generated TLS certificate; LAN observers can still see addresses, timing, and frame sizes.
- Device credentials are stored with Windows DPAPI and Android Keystore-backed storage.
- Re-pairing revokes the previously paired phone.

## Protocol and Security

- Messages use versioned JSON envelopes and UTF-8 payloads.
- Clipboard payloads are limited to 256 KiB.
- Every transfer carries a random message ID and timestamp.
- Duplicate IDs and messages outside a five-minute window are rejected.
- Received clipboard writes are suppressed from retransmission to avoid loops.
- Secret material and clipboard contents must never be logged.
- Pairing and transfer comparison uses constant-time authentication checks.

## User Experience

- Windows exposes connection state, sync pause/resume, pairing/re-pairing, and last-transfer status.
- Android exposes pairing state, sync pause/resume, and connection status.
- Android uses a foreground service only while continuous sync is enabled.
- Sensitive clipboard contents are not shown by default in application screens or notifications.
- Actionable errors distinguish unpaired, unreachable, rejected, oversized, and expired transfers.

## Explicitly Deferred

- Internet relay, accounts, multi-device pairing, clipboard history, images, files, formatted text, custom keyboard integration, accessibility services, and non-Windows desktop clients.
- Code signing and store publication credentials; distributable development builds are produced locally, while public release signing requires owner-controlled certificates/accounts.
