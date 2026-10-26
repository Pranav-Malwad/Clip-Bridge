# Public Repository Readiness Design

## Intent

Prepare the current ClipBridge source tree for a public GitHub repository without changing product behavior or presenting development binaries as audited releases.

## Goals

- Publish the source under a clear permissive license.
- Prevent local credentials, signing keys, runtime data, and build outputs from being committed.
- Run Windows and Android tests for every pull request and branch push.
- Give contributors accurate build, contribution, privacy, and vulnerability-reporting guidance.
- Record the remaining manual GitHub and release-signing steps.

## Non-goals

- Publishing a GitHub repository or release on the owner's behalf.
- Creating or storing signing credentials.
- Claiming an independent security audit.
- Adding automatic releases or unsigned downloadable binaries.
- Changing ClipBridge networking, pairing, clipboard, or privacy behavior.

## Approach

Three approaches were considered: publish the current tree unchanged, add only a license and ignore rules, or add a complete but compact public-project baseline. The complete baseline is selected because ClipBridge handles clipboard data and benefits from automated validation and an explicit security-reporting process. A larger governance setup and automatic binary releases are deferred until the project has maintainers and owner-controlled signing.

## Design

- Use the MIT License with copyright assigned to ClipBridge contributors.
- Expand `.gitignore` for common environment, certificate, keystore, signing-property, runtime-data, log, and editor artifacts.
- Add a least-privilege GitHub Actions workflow with separate Windows/.NET and Ubuntu/Android jobs.
- Add Dependabot configuration for NuGet, Gradle, and GitHub Actions dependencies.
- Add contribution guidance and GitHub issue templates that prohibit including real clipboard content or pairing QR data.
- Direct vulnerability reporters to GitHub Private Vulnerability Reporting when enabled; otherwise ask them to open a content-free public issue requesting a private channel.
- Document that debug/unsigned packages are local-development artifacts and that public binary distribution requires signing and physical-device validation.

## Constraints and risks

- GitHub repository settings such as visibility, branch protection, private vulnerability reporting, secret scanning, and Dependabot alerts must be enabled by the repository owner after upload.
- CI validates unit tests and compilation, not physical-device behavior, firewall prompts, suspend/resume, or Android background restrictions.
- Public source improves reviewability but does not make the application independently audited.

## Validation

- Scan tracked files and Git history for credential-like values and private-key material.
- Run the .NET test solution.
- Run Android unit tests and assemble the debug APK.
- Validate ignored secret/build examples with `git check-ignore`.
- Review the final diff and confirm no product behavior changed.
