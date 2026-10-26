# Public Repository Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the current ClipBridge source repository safe and understandable to publish publicly without shipping unsigned production binaries.

**Architecture:** Keep product code unchanged. Add repository policy, ignore rules, GitHub automation, dependency monitoring, and accurate release boundaries around the existing Windows and Android builds.

**Tech Stack:** Git, GitHub Actions, Dependabot, .NET 10, JDK 17, Gradle 8.9, Android SDK 35

**Spec:** `docs/superpowers/specs/2026-10-26-public-repository-readiness-design.md`

## Global Constraints

- Do not commit secrets, signing credentials, pairing data, runtime state, or generated packages.
- Do not claim an independent security audit or production signing.
- Do not change application behavior.
- Keep CI permissions read-only and do not expose secrets to pull-request code.

## Review Focus

- Signing-key filenames must be ignored before a contributor can accidentally stage them.
- Test fixtures must be distinguishable from real credentials.
- CI must use the repository's actual .NET, Java, Gradle, and Android versions.
- Public issue templates must warn against attaching clipboard contents or pairing invitations.
- Documentation must distinguish source availability from signed production-release readiness.

---

### Task 1: Repository safety and licensing

**Files:**
- Create: `LICENSE`
- Modify: `.gitignore`
- Modify: `DECISIONS.md`

- [x] Add the MIT License and comprehensive credential/build/runtime ignore rules.
- [x] Record the public-source and signed-release decisions.
- [x] Verify representative sensitive filenames are ignored with `git check-ignore`.

### Task 2: Public collaboration and security documentation

**Files:**
- Create: `CONTRIBUTING.md`
- Create: `.github/ISSUE_TEMPLATE/bug_report.yml`
- Create: `.github/ISSUE_TEMPLATE/feature_request.yml`
- Create: `.github/ISSUE_TEMPLATE/config.yml`
- Create: `.github/pull_request_template.md`
- Modify: `README.md`
- Modify: `SECURITY.md`

- [x] Document scope, builds, privacy boundaries, contribution validation, and private vulnerability reporting.
- [x] Add privacy-safe issue and pull-request templates.
- [x] Review every command and behavioral statement against the repository.

### Task 3: Automated validation and dependency monitoring

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/dependabot.yml`

- [x] Add least-privilege Windows/.NET and Ubuntu/Android CI jobs.
- [x] Add weekly update checks for NuGet, Gradle, and GitHub Actions.
- [x] Run equivalent local test/build commands and review the final diff.
