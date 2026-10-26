# ClipBridge V2: Seamless Trusted Devices

## Intent

Make ClipBridge safe and simple enough to share with a small trusted circle. Pairing happens once; ordinary restarts, sleep, Wi-Fi loss, and local IP changes recover without user action. Security controls stay protective by default without turning routine use into a sequence of prompts.

## Goals

- Keep a trusted Windows/Android relationship until the user explicitly forgets it or app data is removed.
- Authenticate every reconnect and derive a fresh ephemeral encryption key for each connection.
- Make the official QR invitation short-lived; document that photographed credentials require rotation until device-identity enrollment is added.
- Recover from laptop IP changes using local discovery with a saved-address fallback.
- Prevent duplicate Windows processes, start cleanly with Windows, and install with searchable shortcuts.
- Block likely sensitive clipboard values by default without storing or logging clipboard content.
- Provide an understandable status-first interface, temporary Private Mode, Manual Mode, and application exclusions.
- Keep all clipboard traffic on the local network and retain no clipboard history.

## Non-goals

- Internet relay, user accounts, remote access, or NAT traversal.
- Claiming protection on a device already compromised by malware or an untrusted accessibility service.
- Password-manager-grade sensitive-text classification; detection is a safety net, not a guarantee.
- Public-store publication or purchasing code-signing certificates on the owner's behalf.
- Images, files, rich text, or multi-device synchronization in this release.

## Selected Approach

Use a persistent OS-protected trust secret for mutual authentication, but never use it directly to encrypt clipboard payloads. Each WebSocket connection performs an authenticated ephemeral ECDH P-256 handshake and derives directional AES-256-GCM session keys using HKDF-SHA256. P-256 is selected instead of X25519 because it is supported by the existing Windows and Android API 29 baselines without adding a cryptographic provider.

The QR carries a random trust secret, pairing identifier, address, port, and expiration. The official Android client rejects it after expiry. The same secret becomes the OS-protected reconnect credential on both devices. Re-pairing rotates the credential and revokes the old phone. A photographed QR can still be used by a custom client until rotation; binding trust to a hardware-backed client identity is a required hardening step before public distribution.

Alternatives considered:

- Continue encrypting directly with the QR secret: simplest, but one key protects every session and a photographed QR remains useful.
- Cloud identity and relay: reconnects across any network, but introduces accounts, server retention, cost, and a much larger privacy boundary.
- X25519/Noise framework: strong and elegant, but requires an additional compatible provider for Android 10 and is disproportionate for this two-device local app.

## Trust and Session Protocol

1. Android opens `/clipbridge` and sends a bounded `ClientHello` containing protocol version, pairing ID, random challenge, ephemeral P-256 public key, and HMAC-SHA256 over canonical hello fields using the trust secret.
2. Windows rejects unknown, expired, already-consumed bootstrap identities, invalid MACs, malformed keys, excessive attempts, or hellos not completed within five seconds.
3. Windows generates its own ephemeral key and random challenge, derives shared ECDH material, and uses HKDF-SHA256 with both challenges and the pairing ID to derive independent Windows-to-Android and Android-to-Windows keys.
4. Windows returns an authenticated `ServerHello`. Android verifies it before considering the session connected.
5. Clipboard envelopes continue to carry authenticated version, ID, timestamp, direction, nonce, ciphertext, and tag, but use the appropriate directional session key.
6. Pairing completion is persisted only after the authenticated handshake succeeds. Session keys are erased when the socket closes.

Replay IDs are retained for the life of each session. Handshake challenges are random and reconnecting yields different session keys, so frames captured from an earlier session cannot authenticate in a later one; cross-restart replay persistence is therefore unnecessary.

## Discovery and Reconnection

Android first tries the last successful address. On failure it sends a small UDP discovery probe on the LAN; Windows replies with its current private IPv4 address and port. The response carries an HMAC made with the trust secret, so an untrusted device cannot redirect the client. Android stores the last successful address and uses exponential backoff capped at 30 seconds. Network changes trigger an immediate discovery attempt. Discovery never exposes the trust secret or clipboard content.

## Privacy Policy

Smart Sync is the default. Windows copies are evaluated in memory before transfer:

- Block common private-key/recovery-phrase markers, likely payment-card values that pass a Luhn check, one-time-code labels, and password-like high-entropy single tokens.
- Block copies whose foreground process is on the built-in deny list (common password managers) or the user's exclusion list.
- Show only a generic local status such as `Sensitive text was not shared`; never show the blocked value.

Manual Mode disables automatic Windows clipboard sending; a deliberate `Send clipboard now` action and configurable global shortcut remain available. The default shortcut is Ctrl+Alt+S. Private Mode pauses all transfer for 10 minutes or until resumed. Timed pauses resume automatically and persist safely across restarts. Detection can produce false positives and is documented as defense in depth.

Received Android text is kept in a short-lived in-process vault for the notification Copy action. The `PendingIntent` contains only an opaque identifier, and entries expire after five minutes. Clipboard content, pairing secrets, and session keys are never logged or placed in analytics/crash metadata.

## Network Hardening

- Accept only WebSocket upgrade requests on the ClipBridge path.
- Five-second authentication deadline, bounded frames, connection cap, and per-address authentication throttling.
- Discovery accepts only private/local addresses and authenticated responses.
- Installer firewall rules are limited to Windows Private profiles.
- Local-only remains the default and there is no router-port-forwarding guidance.

Binding to all local interfaces remains necessary for Wi-Fi address changes; exposure is constrained by Private-profile firewall rules and authentication rather than a fragile single-address bind.

## User Experience

Windows enforces one running instance. Launching again restores the existing window instead of competing for the port. The main screen prioritizes one status card (`Connected`, `Reconnecting`, `Private Mode`, or `Action required`), trusted-device details, and one context-appropriate primary action. Pairing QR is shown only while an invitation is active. Advanced privacy and network controls are grouped below the everyday status.

Android uses a scrollable, safe-area-aware Material 3 screen with a large status card, trusted-PC name, one primary sync action, a Private Mode control, and progressive disclosure for advanced settings. Buttons retain at least 48 dp touch targets, content supports large text and landscape, and colors come from semantic theme tokens in light and dark modes.

## Packaging and Release

- Windows ships through a scripted per-user installer that creates Start Menu and optional startup shortcuts, registers Private-profile firewall access, and supplies uninstall/repair behavior. A production publisher should code-sign the installer and executable.
- Android produces a release-ready APK configuration that requires an owner-supplied protected signing key. Debug artifacts remain clearly labeled.
- Updates, when added, must use an HTTPS manifest with a pinned signing public key and verify package signatures before installation. Silent unsigned updating is prohibited.
- The repository includes a privacy notice, threat model, and release checklist.

## Migration

Existing V1 pairings are intentionally invalidated once because V1 lacks device/session metadata. On first V2 launch, both apps explain that a one-time security upgrade requires a fresh scan. Subsequent upgrades preserve the trusted relationship.

## Validation

- Cross-language handshake fixtures prove identical canonical MACs and derived keys.
- Unit tests cover QR expiry/consumption, invalid hello MACs, key separation, replay persistence, discovery authentication, throttling, privacy classification, timed pause, and notification-vault expiry.
- Integration tests cover authenticated connect, reconnect with a fresh session key, attacker rejection, and single-client replacement.
- Build and lint both platforms, launch the packaged Windows app, verify single-instance activation, and inspect Android signing.
- A physical Windows/Android matrix remains required before general distribution: restart, sleep/resume, Wi-Fi toggle, DHCP address change, router isolation, Android battery restriction, large text, dark mode, and notification permission denied.

