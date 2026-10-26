using ClipBridge.Windows;
using System.Windows.Forms;
using Xunit;

namespace ClipBridge.Windows.Tests;

public sealed class ShortcutGestureTests
{
    [Theory]
    [InlineData("Ctrl+Alt+S", HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.S)]
    [InlineData("ctrl + shift + c", HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.C)]
    public void ParsesSafeShortcut(string value, HotkeyModifiers modifiers, Keys key)
    {
        Assert.True(ShortcutGesture.TryParse(value, out var gesture));
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(key, gesture.Key);
    }

    [Theory]
    [InlineData("Ctrl+C")]
    [InlineData("Alt+F4")]
    [InlineData("S")]
    [InlineData("Ctrl+Alt+Shift")]
    public void RejectsUnsafeOrIncompleteShortcut(string value) =>
        Assert.False(ShortcutGesture.TryParse(value, out _));
}
