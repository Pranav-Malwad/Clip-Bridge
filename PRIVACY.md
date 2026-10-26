# ClipBridge Privacy

ClipBridge transfers plain text directly between one paired Windows PC and Android phone on the same local network. It has no account, cloud relay, analytics, advertising, or clipboard history.

Clipboard text is held only as needed for delivery. Windows evaluates copies in memory and blocks common secret patterns and excluded password-manager processes. Android keeps received text in a five-minute in-process vault for its notification Copy action; the notification intent contains only an opaque identifier. Clipboard content is not written to logs or application storage.

Pairing credentials are protected by Windows DPAPI and Android Keystore-backed encrypted preferences. Each connection derives fresh directional encryption keys through an authenticated ephemeral ECDH handshake. Network observers can still see device addresses, timing, and approximate transfer sizes.

ClipBridge cannot protect text from malware, another application that can read the operating-system clipboard, a compromised device, screenshots, or a person with access to an unlocked device. Sensitive-content detection is a safety net and can miss secrets or block ordinary text. Use Private Mode or Manual Mode for particularly sensitive work.

Never forward ClipBridge ports through a router or expose them to the public internet.
