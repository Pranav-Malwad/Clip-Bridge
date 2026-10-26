using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClipBridge.Protocol;

public sealed record DiscoveryRequest(int Version, string PairingId, string Challenge);
public sealed record DiscoveryResponse(int Version, string PairingId, string Challenge, string Host, int Port, long ExpiresUnixSeconds, string Mac);

public static class DiscoveryProtocol
{
    public const int Version = 2;
    public const int Port = 24892;

    public static DiscoveryRequest CreateRequest(string pairingId, byte[]? challenge = null) =>
        new(Version, pairingId, Convert.ToBase64String(challenge ?? RandomNumberGenerator.GetBytes(16)));

    public static DiscoveryResponse CreateResponse(DiscoveryRequest request, string host, int port, long expires, ReadOnlySpan<byte> secret)
    {
        var unsigned = new DiscoveryResponse(Version, request.PairingId, request.Challenge, host, port, expires, "");
        return unsigned with { Mac = ComputeMac(secret, Canonical(unsigned)) };
    }

    public static bool VerifyResponse(DiscoveryRequest request, DiscoveryResponse response, long now, ReadOnlySpan<byte> secret)
    {
        if (response.Version != Version || response.PairingId != request.PairingId || response.Challenge != request.Challenge ||
            response.Port is < 1 or > 65535 || response.ExpiresUnixSeconds < now || response.ExpiresUnixSeconds > now + 60 ||
            !IsPrivateAddress(response.Host)) return false;
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(response.Mac), Convert.FromBase64String(ComputeMac(secret, Canonical(response)))); }
        catch (FormatException) { return false; }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    public static T? Deserialize<T>(string value) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException) { return null; }
    }

    private static bool IsPrivateAddress(string host)
    {
        if (!IPAddress.TryParse(host, out var address) || address.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 || bytes[0] == 127 || (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31);
    }

    private static string Canonical(DiscoveryResponse r) => $"{r.Version}|{r.PairingId}|{r.Challenge}|{r.Host}|{r.Port}|{r.ExpiresUnixSeconds}";
    private static string ComputeMac(ReadOnlySpan<byte> secret, string value) =>
        Convert.ToBase64String(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(value)));
}
