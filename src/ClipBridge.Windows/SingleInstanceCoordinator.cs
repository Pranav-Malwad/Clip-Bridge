namespace ClipBridge.Windows;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Semaphore _semaphore;
    private readonly EventWaitHandle _activation;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _listener;

    public SingleInstanceCoordinator(string name)
    {
        _semaphore = new Semaphore(1, 1, $"Local\\{name}.Semaphore");
        IsPrimary = _semaphore.WaitOne(0);
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{name}.Activate");
    }

    public bool IsPrimary { get; }

    public void StartListening(Action activated)
    {
        if (!IsPrimary || _listener is not null) return;
        _listener = Task.Run(() =>
        {
            var handles = new WaitHandle[] { _activation, _stopping.Token.WaitHandle };
            while (WaitHandle.WaitAny(handles) == 0)
            {
                try { activated(); }
                catch (InvalidOperationException) { }
            }
        });
    }

    public void SignalPrimary() => _activation.Set();

    public void Dispose()
    {
        _stopping.Cancel();
        try { _listener?.Wait(TimeSpan.FromSeconds(1)); } catch (AggregateException) { }
        _activation.Dispose();
        _stopping.Dispose();
        if (IsPrimary) _semaphore.Release();
        _semaphore.Dispose();
    }
}
