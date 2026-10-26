# ClipBridge Security Policy

ClipBridge is designed for trusted personal devices on a private local network. It does not defend a compromised operating system, an unlocked device, or another application with legitimate clipboard access.

## Supported versions

Security fixes are provided on the latest source revision. Until signed production packages are published, downloaded debug or unsigned builds are development artifacts rather than supported releases.

## Reporting a vulnerability

Use GitHub's **Report a vulnerability** button in the repository's **Security** tab. This creates a private security advisory visible only to the reporter and repository maintainers.

If private vulnerability reporting has not yet been enabled, open a public issue containing only the words **Private security contact requested**. Do not describe the vulnerability publicly. A maintainer will arrange a private channel.

Include affected versions, impact, reproduction steps using harmless sample data, and suggested mitigations when available. Never include real clipboard content, credentials, pairing QR codes or URIs, signing material, or other personal data.

Maintainers should acknowledge a private report within seven days and keep the reporter informed as investigation proceeds. A fix timeline depends on severity and reproducibility; no universal resolution deadline is promised.

## Release checklist

Before sharing a release:

- Run `scripts/build.ps1` from a clean commit and complete the physical-device matrix in the V2 design.
- Sign the Windows executable/installer with the publisher's certificate.
- Supply Android release keys only through local environment variables; never commit them.
- Verify signatures and publish SHA-256 checksums through a trusted channel.
- Confirm the Windows network is Private and firewall rules are Private-profile only.
- Never describe a debug APK as a production-secure release.
- Arrange an independent security review before describing the app as suitable for high-value secrets.
- Publish source and release artifacts from a clean, reviewed commit.
- Enable GitHub secret scanning, Dependabot alerts, private vulnerability reporting, and branch protection.

The complete data-handling boundaries are documented in [`PRIVACY.md`](PRIVACY.md).
