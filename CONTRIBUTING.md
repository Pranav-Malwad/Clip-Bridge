# Contributing to ClipBridge

Thank you for helping improve ClipBridge. The project currently supports one Windows x64 computer and one Android device on the same private local network. Keep changes inside that scope unless a design proposal explicitly changes it.

## Before opening an issue

- Search existing issues first.
- Do not post clipboard contents, passwords, recovery phrases, pairing QR codes, pairing URIs, signing keys, private IP configuration, or diagnostic data containing personal information.
- Report suspected vulnerabilities using the private process in [`SECURITY.md`](SECURITY.md), not a public issue.

## Development setup

Requirements and the complete build command are documented in [`README.md`](README.md). The standard validation command on Windows is:

```powershell
./scripts/build.ps1
```

This runs the .NET and Android unit tests, publishes the self-contained Windows development build, assembles the Android debug APK, and writes checksums under the ignored `dist/` directory.

## Pull requests

- Keep changes focused and preserve unrelated work.
- Add or update tests for behavior changes.
- Run the relevant .NET and Android tests before submitting.
- Update `README.md`, `PRIVACY.md`, `SECURITY.md`, or `DECISIONS.md` when behavior or security boundaries change.
- Never add telemetry, cloud transfer, clipboard history, or secret logging without an approved design and explicit privacy review.
- Never commit certificates, keystores, environment files, runtime pairing state, or real clipboard samples.

By contributing, you agree that your contribution is licensed under the repository's [MIT License](LICENSE).
