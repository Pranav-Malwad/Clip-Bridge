using ClipBridge.Protocol;
using Xunit;

namespace ClipBridge.Protocol.Tests;

public sealed class DiscoveryProtocolTests
{
    private static readonly byte[] Secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public void AuthenticatedPrivateAddressResponseIsAccepted()
    {
        var request = DiscoveryProtocol.CreateRequest("device-123", new byte[16]);
        var response = DiscoveryProtocol.CreateResponse(request, "192.168.1.25", 24891, 1_800_000_030, Secret);

        Assert.True(DiscoveryProtocol.VerifyResponse(request, response, 1_800_000_000, Secret));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("224.0.0.1")]
    public void PublicOrMulticastAddressIsRejected(string host)
    {
        var request = DiscoveryProtocol.CreateRequest("device-123", new byte[16]);
        var response = DiscoveryProtocol.CreateResponse(request, host, 24891, 1_800_000_030, Secret);

        Assert.False(DiscoveryProtocol.VerifyResponse(request, response, 1_800_000_000, Secret));
    }

    [Fact]
    public void TamperedResponseIsRejected()
    {
        var request = DiscoveryProtocol.CreateRequest("device-123", new byte[16]);
        var response = DiscoveryProtocol.CreateResponse(request, "192.168.1.25", 24891, 1_800_000_030, Secret);
        Assert.False(DiscoveryProtocol.VerifyResponse(request, response with { Host = "192.168.1.99" }, 1_800_000_000, Secret));
    }
}
