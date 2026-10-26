using System.Net.WebSockets;
using System.Text;
using ClipBridge.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClipBridge.Windows;

public sealed class ClipBridgeServer(byte[] secret, int port) : IAsyncDisposable
{
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly InboundMessageProcessor _processor = new(secret);
    private WebApplication? _application;
    private WebSocket? _client;

    public event Action<string>? TextReceived;
    public event Action<bool>? ConnectionChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        builder.Services.AddRouting();
        var application = builder.Build();
        application.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
        application.Map("/clipbridge", HandleConnectionAsync);
        _application = application;
        await application.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var socket = _client;
        if (socket?.State != WebSocketState.Open)
        {
            return;
        }

        var envelope = ClipboardProtocol.Encrypt(text, TransferDirection.WindowsToAndroid, secret, DateTimeOffset.UtcNow);
        var bytes = Encoding.UTF8.GetBytes(ClipboardProtocol.Serialize(envelope));
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async Task HandleConnectionAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var previous = Interlocked.Exchange(ref _client, socket);
        if (previous is { State: WebSocketState.Open })
        {
            await previous.CloseAsync(WebSocketCloseStatus.PolicyViolation, "A newer paired device connected.", CancellationToken.None)
                .ConfigureAwait(false);
        }

        ConnectionChanged?.Invoke(true);
        var buffer = new byte[ClipboardProtocol.MaxPayloadBytes * 2];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, context.RequestAborted).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                            .ConfigureAwait(false);
                        return;
                    }

                    if (message.Length + result.Count > buffer.Length)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Frame too large.", CancellationToken.None)
                            .ConfigureAwait(false);
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                var decoded = _processor.Process(Encoding.UTF8.GetString(message.ToArray()));
                if (decoded.IsSuccess && decoded.Text is not null)
                {
                    TextReceived?.Invoke(decoded.Text);
                }
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            Interlocked.CompareExchange(ref _client, null, socket);
            ConnectionChanged?.Invoke(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        var socket = Interlocked.Exchange(ref _client, null);
        if (socket is { State: WebSocketState.Open })
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "ClipBridge is closing.", CancellationToken.None)
                .ConfigureAwait(false);
        }

        if (_application is not null)
        {
            await _application.StopAsync().ConfigureAwait(false);
            await _application.DisposeAsync().ConfigureAwait(false);
        }

        _sendGate.Dispose();
    }
}
