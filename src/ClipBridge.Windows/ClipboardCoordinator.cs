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
    Func<string, Task> sendAsync)
{
    private string? _lastObserved;
    private string? _suppressOnce;

    public bool IsPaused { get; set; }

    public async Task HandleLocalClipboardChangedAsync()
    {
        if (IsPaused)
        {
            return;
        }

        var text = clipboard.GetText();
        if (string.IsNullOrEmpty(text) || text == _lastObserved)
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
