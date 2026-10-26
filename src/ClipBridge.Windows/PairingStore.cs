using System.Security.Cryptography;

namespace ClipBridge.Windows;

public interface ISecretProtector
{
    byte[] Protect(byte[] secret);
    byte[] Unprotect(byte[] protectedSecret);
}

public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = "ClipBridge.Pairing.v1"u8.ToArray();

    public byte[] Protect(byte[] secret) =>
        ProtectedData.Protect(secret, Entropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] protectedSecret) =>
        ProtectedData.Unprotect(protectedSecret, Entropy, DataProtectionScope.CurrentUser);
}

public sealed class PairingStore(string path, ISecretProtector protector)
{
    public byte[] LoadOrCreate()
    {
        if (File.Exists(path))
        {
            var secret = protector.Unprotect(File.ReadAllBytes(path));
            if (secret.Length == 32)
            {
                return secret;
            }
        }

        return Rotate();
    }

    public byte[] Rotate()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, protector.Protect(secret));
        return secret;
    }
}
