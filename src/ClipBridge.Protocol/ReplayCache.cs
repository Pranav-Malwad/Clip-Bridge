namespace ClipBridge.Protocol;

public sealed class ReplayCache
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Queue<(string Id, long Timestamp)> _ordered = new();
    private readonly object _gate = new();

    public bool TryRemember(string messageId, long timestampUnixSeconds, long nowUnixSeconds)
    {
        lock (_gate)
        {
            while (_ordered.TryPeek(out var item) && nowUnixSeconds - item.Timestamp > ClipboardProtocol.AllowedAge.TotalSeconds)
            {
                _ordered.Dequeue();
                _seen.Remove(item.Id);
            }

            if (!_seen.Add(messageId))
            {
                return false;
            }

            _ordered.Enqueue((messageId, timestampUnixSeconds));
            return true;
        }
    }
}
