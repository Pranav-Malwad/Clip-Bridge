using System.Text.RegularExpressions;

namespace ClipBridge.Windows;

public enum ContentDecision { Allow, BlockSensitive, BlockExcludedApplication }

public sealed partial class SensitiveContentPolicy(IEnumerable<string>? excludedProcesses = null)
{
    private readonly HashSet<string> _excluded = new(
        excludedProcesses ?? ["1password", "bitwarden", "keepass", "keepassxc", "lastpass"],
        StringComparer.OrdinalIgnoreCase);

    public ContentDecision Evaluate(string text, string? sourceProcess)
    {
        if (!string.IsNullOrWhiteSpace(sourceProcess) && _excluded.Contains(sourceProcess))
            return ContentDecision.BlockExcludedApplication;

        var trimmed = text.Trim();
        if (PrivateMarker().IsMatch(trimmed) || OtpLabel().IsMatch(trimmed) || ContainsPaymentCard(trimmed) || LooksLikeSecretToken(trimmed))
            return ContentDecision.BlockSensitive;
        return ContentDecision.Allow;
    }

    private static bool ContainsPaymentCard(string text)
    {
        foreach (Match match in CardCandidate().Matches(text))
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is >= 13 and <= 19 && PassesLuhn(digits)) return true;
        }
        return false;
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0; var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var value = digits[i] - '0';
            if (alternate && (value *= 2) > 9) value -= 9;
            sum += value; alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    private static bool LooksLikeSecretToken(string text)
    {
        if (text.Length is < 32 or > 256 || text.Contains("://") || text.Any(char.IsWhiteSpace)) return false;
        if (!text.All(ch => char.IsLetterOrDigit(ch) || ch is '+' or '/' or '_' or '-' or '=')) return false;
        return text.Any(char.IsLower) && text.Any(char.IsUpper) && text.Any(char.IsDigit);
    }

    [GeneratedRegex("BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY|seed phrase|recovery phrase|secret recovery phrase", RegexOptions.IgnoreCase)]
    private static partial Regex PrivateMarker();
    [GeneratedRegex("(?:verification|security|one[- ]?time|otp|2fa|authentication)\\s+(?:code|pin)?\\D{0,12}\\d{4,8}", RegexOptions.IgnoreCase)]
    private static partial Regex OtpLabel();
    [GeneratedRegex("(?<!\\d)(?:\\d[ -]?){12,18}\\d(?!\\d)")]
    private static partial Regex CardCandidate();
}
