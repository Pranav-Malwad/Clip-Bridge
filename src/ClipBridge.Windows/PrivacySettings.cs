using System.Text.Json;

namespace ClipBridge.Windows;

public sealed record PrivacySettings(bool ManualMode, bool Paused, DateTimeOffset? PauseUntil)
{
    public static PrivacySettings Default { get; } = new(false, false, null);
    public bool IsPaused(DateTimeOffset now) => Paused || (PauseUntil is not null && now < PauseUntil);
}

public sealed class PrivacySettingsStore(string path)
{
    public PrivacySettings Load()
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<PrivacySettings>(File.ReadAllText(path)) ?? PrivacySettings.Default : PrivacySettings.Default; }
        catch (Exception error) when (error is IOException or JsonException) { return PrivacySettings.Default; }
    }

    public void Save(PrivacySettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
        File.Move(temporary, path, true);
    }
}
