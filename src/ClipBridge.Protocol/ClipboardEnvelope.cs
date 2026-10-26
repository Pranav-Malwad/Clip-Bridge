namespace ClipBridge.Protocol;

public enum TransferDirection
{
    WindowsToAndroid,
    AndroidToWindows
}

public sealed record ClipboardEnvelope(
    int Version,
    string MessageId,
    long TimestampUnixSeconds,
    TransferDirection Direction,
    string Nonce,
    string Ciphertext,
    string Tag);

public enum DecryptFailure
{
    None,
    InvalidEnvelope,
    AuthenticationFailed,
    Expired,
    Replay,
    PayloadTooLarge
}

public sealed record DecryptResult(string? Text, DecryptFailure Failure)
{
    public bool IsSuccess => Failure == DecryptFailure.None;
    public static DecryptResult Success(string text) => new(text, DecryptFailure.None);
    public static DecryptResult Failed(DecryptFailure failure) => new(null, failure);
}

public sealed class PayloadTooLargeException(int actualBytes)
    : Exception($"Clipboard payload is {actualBytes} bytes; the limit is {ClipboardProtocol.MaxPayloadBytes} bytes.");
