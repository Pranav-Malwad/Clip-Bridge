using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipBridge.Protocol;

public static class ClipboardProtocol
{
    public const int CurrentVersion = 1;
    public const int MaxPayloadBytes = 256 * 1024;
    public static readonly TimeSpan AllowedAge = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static ClipboardEnvelope Encrypt(
        string text,
        TransferDirection direction,
        ReadOnlySpan<byte> secret,
        DateTimeOffset now)
    {
        ValidateSecret(secret);
        var plaintext = Encoding.UTF8.GetBytes(text);
        if (plaintext.Length > MaxPayloadBytes)
        {
            throw new PayloadTooLargeException(plaintext.Length);
        }

        var id = Guid.NewGuid().ToString("N");
        var timestamp = now.ToUnixTimeSeconds();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        var aad = BuildAdditionalData(CurrentVersion, id, timestamp, direction);
        using var aes = new AesGcm(secret, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

        return new ClipboardEnvelope(
            CurrentVersion,
            id,
            timestamp,
            direction,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(tag));
    }

    public static DecryptResult Decrypt(
        ClipboardEnvelope envelope,
        ReadOnlySpan<byte> secret,
        DateTimeOffset now,
        ReplayCache replayCache)
    {
        ValidateSecret(secret);
        if (envelope.Version != CurrentVersion ||
            !Guid.TryParseExact(envelope.MessageId, "N", out _) ||
            string.IsNullOrWhiteSpace(envelope.Nonce) ||
            string.IsNullOrWhiteSpace(envelope.Tag))
        {
            return DecryptResult.Failed(DecryptFailure.InvalidEnvelope);
        }

        var nowSeconds = now.ToUnixTimeSeconds();
        if (Math.Abs(nowSeconds - envelope.TimestampUnixSeconds) > AllowedAge.TotalSeconds)
        {
            return DecryptResult.Failed(DecryptFailure.Expired);
        }

        try
        {
            var nonce = Convert.FromBase64String(envelope.Nonce);
            var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
            var tag = Convert.FromBase64String(envelope.Tag);
            if (nonce.Length != 12 || tag.Length != 16)
            {
                return DecryptResult.Failed(DecryptFailure.InvalidEnvelope);
            }

            if (ciphertext.Length > MaxPayloadBytes)
            {
                return DecryptResult.Failed(DecryptFailure.PayloadTooLarge);
            }

            var plaintext = new byte[ciphertext.Length];
            var aad = BuildAdditionalData(envelope.Version, envelope.MessageId, envelope.TimestampUnixSeconds, envelope.Direction);
            using var aes = new AesGcm(secret, tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);

            if (!replayCache.TryRemember(envelope.MessageId, envelope.TimestampUnixSeconds, nowSeconds))
            {
                return DecryptResult.Failed(DecryptFailure.Replay);
            }

            return DecryptResult.Success(new UTF8Encoding(false, true).GetString(plaintext));
        }
        catch (FormatException)
        {
            return DecryptResult.Failed(DecryptFailure.InvalidEnvelope);
        }
        catch (DecoderFallbackException)
        {
            return DecryptResult.Failed(DecryptFailure.InvalidEnvelope);
        }
        catch (CryptographicException)
        {
            return DecryptResult.Failed(DecryptFailure.AuthenticationFailed);
        }
    }

    public static string Serialize(ClipboardEnvelope envelope) => JsonSerializer.Serialize(envelope, JsonOptions);

    public static ClipboardEnvelope? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ClipboardEnvelope>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static byte[] BuildAdditionalData(int version, string id, long timestamp, TransferDirection direction) =>
        Encoding.UTF8.GetBytes($"{version}|{id}|{timestamp}|{direction}");

    private static void ValidateSecret(ReadOnlySpan<byte> secret)
    {
        if (secret.Length != 32)
        {
            throw new ArgumentException("Pairing secret must be exactly 32 bytes.", nameof(secret));
        }
    }
}
