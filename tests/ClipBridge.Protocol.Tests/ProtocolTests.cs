using System.Text;
using ClipBridge.Protocol;
using Xunit;

namespace ClipBridge.Protocol.Tests;

public sealed class ProtocolTests
{
    private static readonly byte[] Secret = Convert.FromHexString(
        "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F");

    [Fact]
    public void EncryptThenDecryptPreservesUnicodeText()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var envelope = ClipboardProtocol.Encrypt("नमस्ते 👋", TransferDirection.WindowsToAndroid, Secret, now);
        var result = ClipboardProtocol.Decrypt(envelope, Secret, now, new ReplayCache());

        Assert.True(result.IsSuccess);
        Assert.Equal("नमस्ते 👋", result.Text);
    }

    [Fact]
    public void PayloadLimitUsesUtf8Bytes()
    {
        var withinLimit = string.Concat(Enumerable.Repeat("😀", ClipboardProtocol.MaxPayloadBytes / 4));
        var tooLarge = withinLimit + "a";

        Assert.Equal(ClipboardProtocol.MaxPayloadBytes, Encoding.UTF8.GetByteCount(withinLimit));
        Assert.Throws<PayloadTooLargeException>(() => ClipboardProtocol.Encrypt(
            tooLarge, TransferDirection.WindowsToAndroid, Secret, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ModifiedCiphertextIsRejected()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var envelope = ClipboardProtocol.Encrypt("private", TransferDirection.AndroidToWindows, Secret, now);
        var bytes = Convert.FromBase64String(envelope.Ciphertext);
        bytes[0] ^= 0x01;
        envelope = envelope with { Ciphertext = Convert.ToBase64String(bytes) };

        var result = ClipboardProtocol.Decrypt(envelope, Secret, now, new ReplayCache());

        Assert.Equal(DecryptFailure.AuthenticationFailed, result.Failure);
    }

    [Fact]
    public void ReplayIsRejected()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var envelope = ClipboardProtocol.Encrypt("once", TransferDirection.AndroidToWindows, Secret, now);
        var cache = new ReplayCache();

        Assert.True(ClipboardProtocol.Decrypt(envelope, Secret, now, cache).IsSuccess);
        Assert.Equal(DecryptFailure.Replay, ClipboardProtocol.Decrypt(envelope, Secret, now, cache).Failure);
    }

    [Fact]
    public void ExpiredMessageIsRejected()
    {
        var sent = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var envelope = ClipboardProtocol.Encrypt("late", TransferDirection.AndroidToWindows, Secret, sent);

        var result = ClipboardProtocol.Decrypt(envelope, Secret, sent.AddMinutes(6), new ReplayCache());

        Assert.Equal(DecryptFailure.Expired, result.Failure);
    }

    [Fact]
    public void PairingUriRoundTripsIpv4HostAndSecret()
    {
        var pairing = new PairingInfo("Laptop", "192.168.1.20", 24891, Secret);
        var encoded = PairingUri.Format(pairing);

        var parsed = PairingUri.Parse(encoded);

        Assert.Equal(pairing.Name, parsed.Name);
        Assert.Equal(pairing.Host, parsed.Host);
        Assert.Equal(pairing.Port, parsed.Port);
        Assert.Equal(pairing.Secret, parsed.Secret);
    }

    [Fact]
    public void DecryptsSharedCrossPlatformFixture()
    {
        var envelope = new ClipboardEnvelope(
            1,
            "00112233445566778899aabbccddeeff",
            1_800_000_000,
            TransferDirection.WindowsToAndroid,
            "AAECAwQFBgcICQoL",
            "LmyifreKsjtv3QQ=",
            "2fzIjtMRKPw1GJs5h9T1MA==");

        var result = ClipboardProtocol.Decrypt(
            envelope,
            Secret,
            DateTimeOffset.FromUnixTimeSeconds(1_800_000_000),
            new ReplayCache());

        Assert.Equal("interop ✓", result.Text);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("clipbridge://pair?host=x&port=0&secret=nope")]
    [InlineData("clipbridge://pair?host=x&port=24891")]
    public void InvalidPairingUriIsRejected(string value)
    {
        Assert.Throws<FormatException>(() => PairingUri.Parse(value));
    }

    [Fact]
    public void NullCiphertextIsRejectedWithoutThrowing()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var envelope = new ClipboardEnvelope(
            1, Guid.NewGuid().ToString("N"), now.ToUnixTimeSeconds(),
            TransferDirection.AndroidToWindows, "AAECAwQFBgcICQoL", null!, "AAAAAAAAAAAAAAAAAAAAAA==");

        var result = ClipboardProtocol.Decrypt(envelope, Secret, now, new ReplayCache());

        Assert.Equal(DecryptFailure.InvalidEnvelope, result.Failure);
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void ExtremeTimestampIsRejectedWithoutOverflow(long timestamp)
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var envelope = new ClipboardEnvelope(
            1, Guid.NewGuid().ToString("N"), timestamp,
            TransferDirection.AndroidToWindows, "AAECAwQFBgcICQoL", "", "AAAAAAAAAAAAAAAAAAAAAA==");

        var result = ClipboardProtocol.Decrypt(envelope, Secret, now, new ReplayCache());

        Assert.Equal(DecryptFailure.Expired, result.Failure);
    }
}
