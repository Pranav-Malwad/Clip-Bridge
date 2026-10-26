# ClipBridge Security and Release Checklist

ClipBridge is designed for trusted personal devices on a private local network. It does not defend a compromised operating system, an unlocked device, or another application with legitimate clipboard access.

Before sharing a release:

- Run `scripts/build.ps1` from a clean commit and complete the physical-device matrix in the V2 design.
- Sign the Windows executable/installer with the publisher's certificate.
- Supply Android release keys only through local environment variables; never commit them.
- Verify signatures and publish SHA-256 checksums through a trusted channel.
- Confirm the Windows network is Private and firewall rules are Private-profile only.
- Never describe a debug APK as a production-secure release.
- Arrange an independent review before handling high-value secrets.

Report security concerns privately and never attach real clipboard secrets to a report.
