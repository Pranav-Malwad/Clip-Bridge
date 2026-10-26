using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClipBridge.Windows;

public static class ClipboardOwnerProcess
{
    public static string? TryGetName()
    {
        try
        {
            var window = GetClipboardOwner();
            _ = GetWindowThreadProcessId(window, out var processId);
            return processId == 0 ? null : Process.GetProcessById((int)processId).ProcessName;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return null; }
    }

    [DllImport("user32.dll")]
    private static extern nint GetClipboardOwner();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
