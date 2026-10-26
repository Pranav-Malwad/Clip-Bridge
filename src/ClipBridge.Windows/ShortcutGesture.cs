namespace ClipBridge.Windows;

[Flags]
public enum HotkeyModifiers : uint
{
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004
}

public sealed record ShortcutGesture(HotkeyModifiers Modifiers, Keys Key)
{
    public static bool TryParse(string? value, out ShortcutGesture gesture)
    {
        gesture = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;
        var modifiers = (HotkeyModifiers)0;
        Keys? key = null;
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) modifiers |= HotkeyModifiers.Control;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= HotkeyModifiers.Alt;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= HotkeyModifiers.Shift;
            else if (key is null && Enum.TryParse<Keys>(part, true, out var parsed) && IsSupportedKey(parsed)) key = parsed;
            else return false;
        }
        if (key is null || !modifiers.HasFlag(HotkeyModifiers.Control) ||
            (!modifiers.HasFlag(HotkeyModifiers.Alt) && !modifiers.HasFlag(HotkeyModifiers.Shift))) return false;
        gesture = new ShortcutGesture(modifiers, key.Value);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        parts.Add(Key.ToString().ToUpperInvariant());
        return string.Join('+', parts);
    }

    private static bool IsSupportedKey(Keys key) => key is >= Keys.A and <= Keys.Z || key is >= Keys.F1 and <= Keys.F12;
}
