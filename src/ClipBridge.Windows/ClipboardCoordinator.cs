using System.Text;
using ClipBridge.Protocol;

namespace ClipBridge.Windows;

public interface IClipboardService
{
    string? GetText();
    void SetText(string text);
}

public sealed class ClipboardCoordinator(
    IClipboardService clipboard,
    Func<string, Task> sendAsync,
    SensitiveContentPolicy? contentPolicy = null,
    Func<string?>? sourceProcess = null)
{
    private string? _lastObserved;
    private string? _suppressOnce;

    public bool IsPaused { get; set; }
    public bool IsManualMode { get; set; }
    public event Action<ContentDecision>? ContentBlocked;

    public async Task HandleLocalClipboardChangedAsync()
        => await SendClipboardAsync(manualOverride: false).ConfigureAwait(true);

    public async Task SendCurrentClipboardAsync()
        => await SendClipboardAsync(manualOverride: true).ConfigureAwait(true);

    private async Task SendClipboardAsync(bool manualOverride)
    {
        if (IsPaused || (IsManualMode && !manualOverride))
        {
            return;
        }

        var text = clipboard.GetText();
        if (string.IsNullOrEmpty(text) || (!manualOverride && text == _lastObserved))
        {
            return;
        }

        _lastObserved = text;
        if (text == _suppressOnce)
        {
            _suppressOnce = null;
            return;
        }

        if (Encoding.UTF8.GetByteCount(text) > ClipboardProtocol.MaxPayloadBytes)
        {
            return;
        }

        var decision = contentPolicy?.Evaluate(text, sourceProcess?.Invoke()) ?? ContentDecision.Allow;
        if (decision != ContentDecision.Allow)
        {
            ContentBlocked?.Invoke(decision);
            return;
        }

        await sendAsync(text).ConfigureAwait(true);
    }

    public bool ApplyRemoteText(string text)
    {
        if (IsPaused || string.IsNullOrEmpty(text) || Encoding.UTF8.GetByteCount(text) > ClipboardProtocol.MaxPayloadBytes)
        {
            return false;
        }

        _suppressOnce = text;
        _lastObserved = null;
        clipboard.SetText(text);
        return true;
    }
}
