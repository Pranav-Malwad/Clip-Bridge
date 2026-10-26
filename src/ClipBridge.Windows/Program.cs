namespace ClipBridge.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new SingleInstanceCoordinator("ClipBridge.Desktop");
        if (!instance.IsPrimary)
        {
            instance.SignalPrimary();
            return;
        }
        ApplicationConfiguration.Initialize();
        using var form = new MainForm();
        instance.StartListening(() => form.BeginInvoke(form.RestoreWindow));
        Application.Run(form);
    }
}
