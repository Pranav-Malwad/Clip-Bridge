# Engineering Decisions

This file records product and engineering decisions made for the initial ClipBridge release.

## V2: Seamless Trusted Devices

- ClipBridge will use a **pair once, trust until revoked** model. Restarting either device, losing Wi-Fi, sleeping the laptop, or receiving a new LAN address must not require pairing again.
- The selected architecture is a persistent OS-protected trust credential plus an authenticated ephemeral P-256 ECDH handshake for every connection. HKDF-SHA256 derives independent directional AES-256-GCM keys. P-256 is used instead of X25519 to retain Android API 29 support without adding a cryptographic provider.
- The pairing QR carries a two-minute invitation expiry enforced by the official Android client. The trusted credential remains protected by DPAPI and Android Keystore-backed storage. A photographed QR is still a credential until full device-identity enrollment is implemented, so rotating pairing remains required after suspected exposure.
- V1 pairings receive a one-time migration reset because the old record lacks invitation and device metadata. Future upgrades preserve trust.
- Authenticated UDP discovery is added so Android can find the paired laptop after DHCP address changes. The saved address remains the fast first attempt and manual pairing remains a recovery path.
- The Windows listener may bind local interfaces for resilience, but distribution creates a **Private-profile-only** firewall rule. Authentication has a five-second deadline, bounded concurrency, and per-address throttling.
- **Smart Sync** is the default. It blocks likely secrets and copies from built-in/user-excluded applications in memory, shows only generic feedback, and never persists blocked content. Detection is explicitly defense in depth and may produce false positives.
- **Manual Mode** and timed **Private Mode** are first-class controls. Pause state and expiry persist, but clipboard values do not.
- Manual Mode includes a configurable Windows-wide deliberate-send shortcut. The default is **Ctrl+Alt+S** because Ctrl+Shift+V commonly means paste-without-formatting. Shortcuts must include Ctrl plus Alt or Shift and one letter/function key to avoid overriding ordinary copy/paste commands.
- Android notification actions carry an opaque, expiring identifier; plaintext clipboard content is retained only in a five-minute in-process vault and never embedded in a `PendingIntent`.
- Windows enforces a single running process; a second launch activates the existing window. The distribution must create searchable Start Menu and optional startup shortcuts.
- The interface is status-first, uses consistent `Connected`, `Reconnecting`, `Private Mode`, and `Action required` vocabulary, progressively discloses advanced controls, supports dark mode and large text, and uses Android touch targets of at least 48 dp.
- No cloud relay, account system, analytics, clipboard history, or internet exposure is introduced. Clipboard contents, trust credentials, and session keys are never logged.
- Public distribution requires owner-controlled Windows code-signing and Android release-signing credentials. Keys are never generated into or committed to the repository. Unsigned/debug packages remain visibly labeled and are not represented as production-secure releases.
- Automatic updates are deferred until they can verify an HTTPS manifest and a pinned publisher signature; unsigned self-update behavior is prohibited.

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
