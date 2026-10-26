using System.Security.Cryptography;

namespace ClipBridge.Protocol;

public static class PairingIdentity
{
    public static string FromSecret(ReadOnlySpan<byte> secret) =>
        Convert.ToHexString(HMACSHA256.HashData(secret, "ClipBridge/pairing-id/v2"u8)).ToLowerInvariant()[..32];
}
