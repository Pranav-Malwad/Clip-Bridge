using System.Net;
using System.Net.Sockets;
using System.Text;
using ClipBridge.Protocol;

namespace ClipBridge.Windows;

public sealed class DiscoveryService(byte[] secret, string pairingId, int servicePort) : IAsyncDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private UdpClient? _udp;
    private Task? _loop;

    public void Start()
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, DiscoveryProtocol.Port));
        _loop = RunAsync(_stopping.Token);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var received = await _udp!.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (received.Buffer.Length > 2048) continue;
                var request = DiscoveryProtocol.Deserialize<DiscoveryRequest>(Encoding.UTF8.GetString(received.Buffer));
                if (request is null || request.Version != DiscoveryProtocol.Version || request.PairingId != pairingId) continue;
                var host = NetworkAddress.FindLanIpv4();
                var response = DiscoveryProtocol.CreateResponse(request, host, servicePort, DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds(), secret);
                var bytes = Encoding.UTF8.GetBytes(DiscoveryProtocol.Serialize(response));
                await _udp.SendAsync(bytes, received.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();
        _udp?.Dispose();
        if (_loop is not null) { try { await _loop.ConfigureAwait(false); } catch (ObjectDisposedException) { } }
        _stopping.Dispose();
    }
}
