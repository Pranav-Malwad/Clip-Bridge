using ClipBridge.Protocol;
using ClipBridge.Windows;
using Xunit;

namespace ClipBridge.Windows.Tests;

public sealed class PairingAndMessageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"clipbridge-{Guid.NewGuid():N}");

    [Fact]
    public void PairingStorePersistsAndRotatesSecret()
    {
        var path = Path.Combine(_directory, "pairing.dat");
        var store = new PairingStore(path, new IdentityProtector());

        var first = store.LoadOrCreate();
        store.MarkPaired();
        var second = store.LoadOrCreate();
        var rotated = store.Rotate();

        Assert.Equal(first, second);
        Assert.NotEqual(first, rotated);
        Assert.Equal(32, rotated.Length);
        Assert.False(store.IsPaired);
    }

    [Fact]
    public void ProcessorAcceptsAuthenticatedAndroidMessageOnlyOnce()
    {
        var secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var envelope = ClipboardProtocol.Encrypt("shared", TransferDirection.AndroidToWindows, secret, now);
        var processor = new InboundMessageProcessor(secret, () => now);

        Assert.Equal("shared", processor.Process(ClipboardProtocol.Serialize(envelope)).Text);
        Assert.Equal(DecryptFailure.Replay, processor.Process(ClipboardProtocol.Serialize(envelope)).Failure);
    }

    [Fact]
    public void ProcessorRejectsWrongDirectionAndMalformedJson()
    {
        var secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var envelope = ClipboardProtocol.Encrypt("wrong way", TransferDirection.WindowsToAndroid, secret, now);
        var processor = new InboundMessageProcessor(secret, () => now);

        Assert.Equal(DecryptFailure.InvalidEnvelope, processor.Process(ClipboardProtocol.Serialize(envelope)).Failure);
        Assert.Equal(DecryptFailure.InvalidEnvelope, processor.Process("not-json").Failure);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private sealed class IdentityProtector : ISecretProtector
    {
        public byte[] Protect(byte[] secret) => secret;
        public byte[] Unprotect(byte[] protectedSecret) => protectedSecret;
    }
}
