using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClipBridge.Protocol;

public sealed record ClientHello(int Version, string PairingId, string Challenge, string PublicKey, string Mac);
public sealed record ServerHello(int Version, string Challenge, string PublicKey, string Mac);
public sealed record SessionKeys(byte[] WindowsToAndroid, byte[] AndroidToWindows);
public sealed record ServerHandshake(ServerHello Hello, SessionKeys Keys);
public sealed record ClientHandshake(SessionKeys? Keys, string? Error)
{
    public bool IsSuccess => Keys is not null;
}

public static class SessionProtocol
{
    public const int CurrentVersion = 2;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ECDiffieHellman CreateEphemeralKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    public static ClientHello CreateClientHello(string pairingId, ReadOnlySpan<byte> trustSecret,
        ECDiffieHellman key, byte[]? challenge = null)
    {
        ValidateSecret(trustSecret);
        var challengeText = Convert.ToBase64String(challenge ?? RandomNumberGenerator.GetBytes(16));
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var unsigned = new ClientHello(CurrentVersion, pairingId, challengeText, publicKey, "");
        return unsigned with { Mac = ComputeMac(trustSecret, ClientCanonical(unsigned)) };
    }

    public static bool VerifyClientHello(ClientHello hello, ReadOnlySpan<byte> trustSecret)
    {
        try
        {
            ValidateSecret(trustSecret);
            if (hello.Version != CurrentVersion || string.IsNullOrWhiteSpace(hello.PairingId) ||
                Convert.FromBase64String(hello.Challenge).Length != 16)
                return false;
            using var imported = CreateEphemeralKey();
            imported.ImportSubjectPublicKeyInfo(Convert.FromBase64String(hello.PublicKey), out _);
            return VerifyMac(trustSecret, ClientCanonical(hello), hello.Mac);
        }
        catch (Exception error) when (error is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    public static ServerHandshake CreateServerHello(ClientHello clientHello, ReadOnlySpan<byte> trustSecret,
        ECDiffieHellman serverKey, byte[]? challenge = null)
    {
        if (!VerifyClientHello(clientHello, trustSecret)) throw new CryptographicException("Client hello authentication failed.");
        var challengeText = Convert.ToBase64String(challenge ?? RandomNumberGenerator.GetBytes(16));
        var publicKey = Convert.ToBase64String(serverKey.ExportSubjectPublicKeyInfo());
        var unsigned = new ServerHello(CurrentVersion, challengeText, publicKey, "");
        var hello = unsigned with { Mac = ComputeMac(trustSecret, ServerCanonical(clientHello, unsigned)) };
        return new ServerHandshake(hello, DeriveKeys(clientHello, hello, serverKey));
    }

    public static ClientHandshake CompleteClientHandshake(ClientHello clientHello, ServerHello serverHello,
        ReadOnlySpan<byte> trustSecret, ECDiffieHellman clientKey)
    {
        try
        {
            ValidateSecret(trustSecret);
            if (serverHello.Version != CurrentVersion || Convert.FromBase64String(serverHello.Challenge).Length != 16 ||
                !VerifyMac(trustSecret, ServerCanonical(clientHello, serverHello), serverHello.Mac))
                return new(null, "Server hello authentication failed.");
            return new(DeriveKeys(clientHello, serverHello, clientKey), null);
        }
        catch (Exception error) when (error is FormatException or CryptographicException or ArgumentException)
        {
            return new(null, "Server hello is invalid.");
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    public static T? Deserialize<T>(string value) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(value, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static SessionKeys DeriveKeys(ClientHello clientHello, ServerHello serverHello, ECDiffieHellman ownKey)
    {
        using var peer = CreateEphemeralKey();
        var peerBytes = Convert.FromBase64String(
            ownKey.PublicKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(Convert.FromBase64String(clientHello.PublicKey))
                ? serverHello.PublicKey : clientHello.PublicKey);
        peer.ImportSubjectPublicKeyInfo(peerBytes, out _);
        var shared = ownKey.DeriveRawSecretAgreement(peer.PublicKey);
        var salt = SHA256.HashData(Encoding.UTF8.GetBytes($"{clientHello.PairingId}|{clientHello.Challenge}|{serverHello.Challenge}"));
        var output = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 64, salt, "ClipBridge/session/v2"u8.ToArray());
        CryptographicOperations.ZeroMemory(shared);
        return new(output[..32], output[32..]);
    }

    private static string ClientCanonical(ClientHello hello) =>
        $"client|{hello.Version}|{hello.PairingId}|{hello.Challenge}|{hello.PublicKey}";
    private static string ServerCanonical(ClientHello client, ServerHello server) =>
        $"server|{server.Version}|{client.PairingId}|{client.Challenge}|{client.PublicKey}|{server.Challenge}|{server.PublicKey}";
    private static string ComputeMac(ReadOnlySpan<byte> secret, string value) =>
        Convert.ToBase64String(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(value)));
    private static bool VerifyMac(ReadOnlySpan<byte> secret, string value, string supplied)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(supplied), HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(value))); }
        catch (FormatException) { return false; }
    }
    private static void ValidateSecret(ReadOnlySpan<byte> secret)
    {
        if (secret.Length != 32) throw new ArgumentException("Trust secret must be exactly 32 bytes.");
    }
}
