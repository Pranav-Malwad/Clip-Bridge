using ClipBridge.Protocol;

namespace ClipBridge.Windows;

public sealed class InboundMessageProcessor(byte[] secret, Func<DateTimeOffset>? clock = null)
{
    private readonly ReplayCache _replayCache = new();
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    public DecryptResult Process(string json)
    {
        var envelope = ClipboardProtocol.Deserialize(json);
        if (envelope is null || envelope.Direction != TransferDirection.AndroidToWindows)
        {
            return DecryptResult.Failed(DecryptFailure.InvalidEnvelope);
        }

        return ClipboardProtocol.Decrypt(envelope, secret, _clock(), _replayCache);
    }
}
