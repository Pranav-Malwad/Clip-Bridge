using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using ClipBridge.Protocol;
using ClipBridge.Windows;
using Xunit;

namespace ClipBridge.Windows.Tests;

public sealed class ServerIntegrationTests
{
    [Fact]
    public async Task WebSocketTransfersAuthenticatedTextInBothDirections()
    {
        var secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var port = FindFreePort();
        await using var server = new ClipBridgeServer(secret, port);
        var receivedByWindows = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.TextReceived += text => receivedByWindows.TrySetResult(text);
        await server.StartAsync(TestContext.Current.CancellationToken);

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/clipbridge"), TestContext.Current.CancellationToken);

        await AuthenticateAsync(client, secret);

        var toWindows = ClipboardProtocol.Encrypt(
            "phone to laptop",
            TransferDirection.AndroidToWindows,
            secret,
            DateTimeOffset.UtcNow);
        await client.SendAsync(
            Encoding.UTF8.GetBytes(ClipboardProtocol.Serialize(toWindows)),
            WebSocketMessageType.Text,
            true,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            "phone to laptop",
            await receivedByWindows.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        await server.SendTextAsync("laptop to phone", TestContext.Current.CancellationToken);
        var buffer = new byte[ClipboardProtocol.MaxPayloadBytes * 2];
        var response = await client.ReceiveAsync(buffer, TestContext.Current.CancellationToken);
        var envelope = ClipboardProtocol.Deserialize(Encoding.UTF8.GetString(buffer, 0, response.Count));
        Assert.NotNull(envelope);
        var decoded = ClipboardProtocol.Decrypt(envelope, secret, DateTimeOffset.UtcNow, new ReplayCache());
        Assert.Equal("laptop to phone", decoded.Text);
    }

    [Fact]
    public async Task UnauthenticatedConnectionCannotDisplacePairedClient()
    {
        var secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var port = FindFreePort();
        await using var server = new ClipBridgeServer(secret, port);
        await server.StartAsync(TestContext.Current.CancellationToken);
        using var paired = new ClientWebSocket();
        await paired.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/clipbridge"), TestContext.Current.CancellationToken);
        await AuthenticateAsync(paired, secret);

        using var attacker = new ClientWebSocket();
        await attacker.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/clipbridge"), TestContext.Current.CancellationToken);
        await server.SendTextAsync("still paired", TestContext.Current.CancellationToken);

        var buffer = new byte[4096];
        var response = await paired.ReceiveAsync(buffer, TestContext.Current.CancellationToken);
        var envelope = ClipboardProtocol.Deserialize(Encoding.UTF8.GetString(buffer, 0, response.Count));
        Assert.NotNull(envelope);
        Assert.Equal("still paired", ClipboardProtocol.Decrypt(envelope, secret, DateTimeOffset.UtcNow, new ReplayCache()).Text);
    }

    private static Task AuthenticateAsync(ClientWebSocket client, byte[] secret)
    {
        var hello = ClipboardProtocol.Encrypt("", TransferDirection.AndroidToWindows, secret, DateTimeOffset.UtcNow);
        return client.SendAsync(
            Encoding.UTF8.GetBytes(ClipboardProtocol.Serialize(hello)),
            WebSocketMessageType.Text,
            true,
            TestContext.Current.CancellationToken);
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
