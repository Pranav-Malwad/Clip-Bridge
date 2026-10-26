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
                try
                {
                    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception error) when (error is WebSocketException or ObjectDisposedException)
                {
                    if (Interlocked.CompareExchange(ref _client, null, socket) == socket)
                    {
                        ConnectionChanged?.Invoke(false);
                    }
                }
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
        var buffer = new byte[ClipboardProtocol.MaxPayloadBytes * 2];
        string? firstMessage;
        try
        {
            firstMessage = await ReceiveTextAsync(socket, buffer, context.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (WebSocketException)
        {
            return;
        }
        if (firstMessage is null)
        {
            return;
        }

        var authentication = _processor.Process(firstMessage);
        if (!authentication.IsSuccess)
        {
            await SafeCloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Authentication failed.").ConfigureAwait(false);
            return;
        }

        var previous = Interlocked.Exchange(ref _client, socket);
        if (previous is { State: WebSocketState.Open })
        {
            await SafeCloseAsync(previous, WebSocketCloseStatus.PolicyViolation, "A newer paired device connected.")
                .ConfigureAwait(false);
        }

        ConnectionChanged?.Invoke(true);
        if (!string.IsNullOrEmpty(authentication.Text))
        {
            TextReceived?.Invoke(authentication.Text);
        }
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var json = await ReceiveTextAsync(socket, buffer, context.RequestAborted).ConfigureAwait(false);
                if (json is null)
                {
                    return;
                }

                var decoded = _processor.Process(json);
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
            if (Interlocked.CompareExchange(ref _client, null, socket) == socket)
            {
                ConnectionChanged?.Invoke(false);
            }
        }
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await SafeCloseAsync(socket, WebSocketCloseStatus.NormalClosure, null).ConfigureAwait(false);
                return null;
            }
            if (message.Length + result.Count > buffer.Length)
            {
                await SafeCloseAsync(socket, WebSocketCloseStatus.MessageTooBig, "Frame too large.").ConfigureAwait(false);
                return null;
            }
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return result.MessageType == WebSocketMessageType.Text
            ? Encoding.UTF8.GetString(message.ToArray())
            : null;
    }

    private static async Task SafeCloseAsync(WebSocket socket, WebSocketCloseStatus status, string? description)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(status, description, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            socket.Abort();
        }
    }

    public async ValueTask DisposeAsync()
    {
        var socket = Interlocked.Exchange(ref _client, null);
        if (socket is { State: WebSocketState.Open })
        {
            await SafeCloseAsync(socket, WebSocketCloseStatus.NormalClosure, "ClipBridge is closing.").ConfigureAwait(false);
        }

        if (_application is not null)
        {
            await _application.StopAsync().ConfigureAwait(false);
            await _application.DisposeAsync().ConfigureAwait(false);
        }

        _sendGate.Dispose();
    }
}
