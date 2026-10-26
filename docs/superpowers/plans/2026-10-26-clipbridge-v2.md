# ClipBridge V2 Trusted Devices Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade ClipBridge to pair once, reconnect automatically, protect each session with ephemeral keys, filter sensitive clipboard content, and ship with approachable interfaces and installation support.

**Architecture:** The existing trust secret becomes an OS-protected authentication credential. Every WebSocket connection performs an authenticated ephemeral P-256 ECDH/HKDF handshake and then uses directional session keys; authenticated UDP discovery repairs address changes. Privacy policy and pause state remain local, while UI and packaging expose safe defaults without clipboard history.

**Tech Stack:** .NET 10/C#/Windows Forms/ASP.NET Core WebSockets, Kotlin/Android API 29+/Jetpack Compose/OkHttp, AES-GCM, ECDH P-256, HKDF-SHA256, xUnit/JUnit, PowerShell installer tooling.

**Spec:** `docs/superpowers/specs/2026-10-26-clipbridge-v2-design.md`

## Global Constraints

- Same-LAN, one trusted Windows PC and one Android phone; no cloud or accounts.
- Plain UTF-8 text only, maximum 256 KiB, with no clipboard history or content logging.
- Android API 29+ and Windows 10 1809+ x64.
- Pair once; restarts, temporary network loss, and DHCP address changes must not require another scan.
- Official QR invitations expire after two minutes; suspected screenshots require pairing rotation until hardware-backed client identity enrollment is implemented.
- Authentication must finish within five seconds; session keys are connection-scoped and directional.
- Smart Sync blocks likely sensitive content by default; classification is defense in depth, not a guarantee.
- Release signing requires owner-controlled credentials and must never embed keys in the repository.

## Review Focus

- A replayed or expired pairing invitation cannot establish a new trusted device while the existing trusted phone can reconnect.
- Client/server canonical handshake bytes and HKDF outputs match across C# and Kotlin for non-ASCII device names and malformed input fails closed.
- Discovery cannot redirect Android without a valid HMAC and ignores public/multicast response addresses.
- Sensitive filtering never logs or displays blocked content, and ordinary URLs/code snippets are not broadly misclassified.
- Process restart preserves trusted pairing, pause expiry, and replay protection without persisting clipboard text.

---

### Task 1: V2 trust and ephemeral session protocol

**Files:**
- Create: `src/ClipBridge.Protocol/SessionProtocol.cs`
- Create: `src/ClipBridge.Protocol/Hkdf.cs`
- Modify: `src/ClipBridge.Protocol/PairingUri.cs`
- Modify: `src/ClipBridge.Windows/PairingStore.cs`
- Modify: `src/ClipBridge.Windows/ClipBridgeServer.cs`
- Create: `android/app/src/main/java/com/clipbridge/app/protocol/SessionProtocol.kt`
- Modify: Android pairing/protocol/store files
- Test: protocol and Windows/Android protocol test projects

**Interfaces:** Produces short-lived `PairingInfo`, authenticated `ClientHello`/`ServerHello`, and `SessionKeys` with independent send/receive keys.

- [ ] Write C# and Kotlin failing tests from fixed handshake/expiry/key-separation fixtures.
- [ ] Run the focused suites and confirm RED for missing V2 APIs.
- [ ] Implement canonical hello authentication, ECDH P-256, HKDF-SHA256, invitation lifecycle, and directional session encryption.
- [ ] Run focused and full suites; confirm GREEN and cross-language fixtures match.
- [ ] Commit the independently working protocol migration.

### Task 2: Session replay state and hardened connection admission

**Files:**
- Modify: `src/ClipBridge.Windows/ClipBridgeServer.cs`
- Modify: Android session handling and `SyncService.kt`
- Test: server integration and Android protocol tests

**Interfaces:** Produces connection-scoped replay stores, five-second authentication cancellation, connection limits, and per-address failure throttling. Fresh ephemeral keys make prior-session frames invalid after reconnect or restart.

- [ ] Write failing session-replay, slow-authentication, and repeated-invalid-authentication tests.
- [ ] Verify expected RED failures.
- [ ] Implement bounded session replay state and admission controls without retaining payloads.
- [ ] Run focused and full suites; confirm GREEN.
- [ ] Commit connection hardening.

### Task 3: Authenticated discovery and seamless reconnect

**Files:**
- Create: Windows `DiscoveryService.cs` and Android `DiscoveryClient.kt`
- Modify: `MainForm.cs`, `SyncService.kt`, manifest/network callbacks
- Test: discovery authentication/address validation tests

**Interfaces:** UDP probe/reply on a dedicated port; reply MAC binds pairing ID, address, port, challenge, and expiry.

- [ ] Write failing tests for valid discovery, tampering, stale response, and non-private address rejection.
- [ ] Verify RED.
- [ ] Implement Windows responder, Android discovery, saved-address update, and immediate network-change reconnect.
- [ ] Run focused/full suites and Android lint.
- [ ] Commit seamless discovery and reconnect.

### Task 4: Privacy controls and transient Android delivery

**Files:**
- Create: Windows `SensitiveContentPolicy.cs`, `PrivacySettings.cs`, foreground-process adapter
- Modify: `ClipboardCoordinator.cs`, `MainForm.cs`
- Create: Android `TransientClipboardVault.kt`
- Modify: `SyncService.kt`, `CopyReceiver.kt`
- Test: Windows policy/settings and Android vault tests

**Interfaces:** `SensitiveContentPolicy.Evaluate(text, sourceProcess)`, persisted sync mode/pause deadline/exclusions, and opaque notification tokens with five-minute TTL.

- [ ] Write failing tests for recovery keys, payment cards, OTP labels, high-entropy tokens, exclusions, false-positive controls, timed pause, and vault expiry.
- [ ] Verify RED.
- [ ] Implement Smart/Manual modes, exclusions, timed Private Mode, generic feedback, and content-free notification intents.
- [ ] Run focused/full suites and lint.
- [ ] Commit privacy controls.

### Task 5: Status-first interface and single-instance Windows lifecycle

**Files:**
- Create: `src/ClipBridge.Windows/SingleInstanceCoordinator.cs`
- Modify: `Program.cs`, `MainForm.cs`, Android `MainActivity.kt`, theme/resources
- Test: coordinator/view-state unit tests where platform-independent

**Interfaces:** A named mutex plus activation signal restores the first Windows window; both platforms render the same status vocabulary and progressive settings.

- [ ] Write failing lifecycle and view-state tests.
- [ ] Verify RED.
- [ ] Implement activation, clearer status cards, trusted-device management, onboarding copy, responsive scrolling, semantic colors, dark mode, 48 dp Android targets, and accessible labels.
- [ ] Run tests, lint, builds, and Windows duplicate-launch smoke test.
- [ ] Commit UX/lifecycle improvements.

### Task 6: Installation, startup, firewall, privacy, and release readiness

**Files:**
- Create: `installer/Install-ClipBridge.ps1`, `installer/Uninstall-ClipBridge.ps1`, privacy/threat/release docs
- Modify: `scripts/build.ps1`, Android Gradle configuration, `README.md`, `DECISIONS.md`
- Test: PowerShell script parsing and isolated install/uninstall smoke checks

**Interfaces:** Produces a per-user Windows installation with Start Menu/startup shortcuts and Private-profile firewall configuration, plus debug and owner-signable Android outputs.

- [ ] Write safe isolated installer validation and verify missing behavior.
- [ ] Implement idempotent install/repair/uninstall, startup preference, private firewall rule, release signing hooks, checksums, and release documentation.
- [ ] Run all tests, lint, Release builds, installer smoke test, signature inspection, and final diff review.
- [ ] Commit distribution readiness.

