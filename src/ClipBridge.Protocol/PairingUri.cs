using System.Globalization;

namespace ClipBridge.Protocol;

public sealed record PairingInfo(string Name, string Host, int Port, byte[] Secret);

public static class PairingUri
{
    public static string Format(PairingInfo pairing)
    {
        Validate(pairing.Host, pairing.Port, pairing.Secret);
        return $"clipbridge://pair?host={Uri.EscapeDataString(pairing.Host)}" +
               $"&port={pairing.Port.ToString(CultureInfo.InvariantCulture)}" +
               $"&secret={Base64UrlEncode(pairing.Secret)}" +
               $"&name={Uri.EscapeDataString(pairing.Name)}";
    }

    public static PairingInfo Parse(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "clipbridge", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "pair", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("Not a ClipBridge pairing URI.");
        }

        var query = ParseQuery(uri.Query);
        if (!query.TryGetValue("host", out var host) ||
            !query.TryGetValue("port", out var portText) ||
            !int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
            !query.TryGetValue("secret", out var secretText))
        {
            throw new FormatException("Pairing URI is incomplete.");
        }

        byte[] secret;
        try
        {
            secret = Base64UrlDecode(secretText);
        }
        catch (FormatException)
        {
            throw new FormatException("Pairing secret is invalid.");
        }

        Validate(host, port, secret);
        query.TryGetValue("name", out var name);
        return new PairingInfo(string.IsNullOrWhiteSpace(name) ? "Windows PC" : name, host, port, secret);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            result[Uri.UnescapeDataString(pair[..separator])] = Uri.UnescapeDataString(pair[(separator + 1)..]);
        }

        return result;
    }

    private static void Validate(string host, int port, byte[] secret)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 255 || port is < 1 or > 65535 || secret.Length != 32)
        {
            throw new FormatException("Pairing parameters are invalid.");
        }
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += new string('=', (4 - normalized.Length % 4) % 4);
        return Convert.FromBase64String(normalized);
    }
}
