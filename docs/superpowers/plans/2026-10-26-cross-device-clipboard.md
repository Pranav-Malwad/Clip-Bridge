# ClipBridge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a working, local-first Windows and Android text clipboard bridge with QR pairing, authenticated transfers, and platform-appropriate clipboard behavior.

**Architecture:** A .NET Windows host owns pairing and the local WebSocket session; a Kotlin Android client connects after QR pairing. Both implement the same bounded, replay-resistant authenticated message protocol, while platform adapters isolate clipboard and lifecycle behavior.

**Tech Stack:** .NET/C#, Windows Forms/native clipboard APIs, ASP.NET Core WebSockets, Kotlin, Android SDK/Jetpack Compose, Gradle, JUnit/xUnit.

**Spec:** `docs/superpowers/specs/2026-10-26-cross-device-clipboard-design.md`

## Global Constraints

- Same-LAN, one Windows device and one Android device.
- Plain UTF-8 text only, maximum 256 KiB.
- Android API 29+ and Windows 10 1809+ x64.
- No clipboard persistence or secret/content logging.
- Reject duplicate IDs and timestamps outside five minutes.
- Use OS-protected credential storage and suppress clipboard loops.

## Review Focus

- A 256 KiB boundary payload is accepted while a one-byte-larger UTF-8 payload is rejected.
- Multibyte UTF-8 text is measured by encoded bytes, not character count.
- Replayed and expired authenticated messages never reach either clipboard.
- Disconnect/reconnect does not retransmit a received clipboard item.
- Malformed share intents and protocol frames fail safely without clipboard mutation.

---

### Task 1: Repository and shared protocol fixtures

**Files:** Create solution/project scaffolding, protocol fixtures, and documentation.

**Interfaces:** Defines envelope field names, canonical authentication bytes, limits, and shared cross-language fixtures.

- [ ] Write protocol fixture tests in both languages and observe missing-implementation failures.
- [ ] Implement canonical envelopes, payload bounds, timestamps, IDs, authentication, and replay cache.
- [ ] Run both protocol suites and confirm shared fixtures pass.

### Task 2: Windows clipboard coordinator

**Files:** Create Windows core, platform adapter, host UI, and tests.

**Interfaces:** Produces `IClipboardService`, `ClipboardCoordinator`, pairing state, and session send/receive operations.

- [ ] Write failing coordinator tests for local send, remote apply, oversize rejection, and loop suppression.
- [ ] Implement the in-memory-testable coordinator and native Windows clipboard adapter.
- [ ] Run Windows tests and confirm the suite passes.

### Task 3: Windows pairing and WebSocket host

**Files:** Create pairing payload, credential protection, local server, QR/status UI, and tests.

**Interfaces:** Produces a QR pairing URI and an authenticated session endpoint consumed by Android.

- [ ] Write failing tests for one-time pairing, revocation, invalid authentication, replay, and expiration.
- [ ] Implement pairing state, DPAPI persistence, bounded WebSocket framing, and host lifecycle.
- [ ] Run Windows tests and build the Windows application.

### Task 4: Android protocol and secure pairing

**Files:** Create Android project, protocol module, pairing repository, QR/manual entry UI, and tests.

**Interfaces:** Consumes the pairing URI and protocol fixtures; produces stored paired-device state.

- [ ] Write failing JVM tests for fixtures, URI validation, bounds, authentication, replay, and expiration.
- [ ] Implement the protocol and Keystore-backed pairing repository.
- [ ] Run Android unit tests.

### Task 5: Android sharing and incoming delivery

**Files:** Create share receiver, sync service, clipboard gateway, notifications, Compose status UI, and tests.

**Interfaces:** Sends shared `text/plain` to Windows and applies valid incoming text with a notification Copy fallback.

- [ ] Write failing tests for valid/malformed shares, paused/unpaired behavior, delivery decisions, and oversize text.
- [ ] Implement Share Sheet handling, foreground connection, clipboard delivery, notifications, and app state.
- [ ] Run Android unit tests and build a debug APK.

### Task 6: Packaging and end-to-end validation

**Files:** Create build scripts, README, troubleshooting guide, icons/resources, and release output folders.

**Interfaces:** Produces a Windows distributable and Android debug APK with reproducible build commands.

- [ ] Run all tests from a clean invocation.
- [ ] Build Windows Release and Android debug artifacts.
- [ ] Inspect artifacts, run a Windows launch smoke test, and document Android device smoke-test steps/limitations.
- [ ] Review the final diff against the spec and `DECISIONS.md`.
