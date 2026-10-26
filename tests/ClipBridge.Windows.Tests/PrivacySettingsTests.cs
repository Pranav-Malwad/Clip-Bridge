using ClipBridge.Windows;
using Xunit;

namespace ClipBridge.Windows.Tests;

public sealed class PrivacySettingsTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"clipbridge-settings-{Guid.NewGuid():N}.json");

    [Fact]
    public void TimedPrivateModePersistsAndExpires()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var store = new PrivacySettingsStore(_path);
        store.Save(new PrivacySettings(false, false, now.AddMinutes(10)));

        var loaded = store.Load();
        Assert.True(loaded.IsPaused(now.AddMinutes(9)));
        Assert.False(loaded.IsPaused(now.AddMinutes(11)));
    }

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }
}
