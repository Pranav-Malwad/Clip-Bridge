namespace ClipBridge.Windows;

public sealed class WindowsClipboardService : IClipboardService
{
    public string? GetText() => Clipboard.ContainsText(TextDataFormat.UnicodeText)
        ? Clipboard.GetText(TextDataFormat.UnicodeText)
        : null;

    public void SetText(string text) => Clipboard.SetText(text, TextDataFormat.UnicodeText);
}
