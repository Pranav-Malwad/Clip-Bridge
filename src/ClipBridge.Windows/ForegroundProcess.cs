using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClipBridge.Windows;

public static class ForegroundProcess
{
    public static string? TryGetName()
    {
        try
        {
            var window = GetForegroundWindow();
            _ = GetWindowThreadProcessId(window, out var processId);
            return processId == 0 ? null : Process.GetProcessById((int)processId).ProcessName;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return null; }
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
