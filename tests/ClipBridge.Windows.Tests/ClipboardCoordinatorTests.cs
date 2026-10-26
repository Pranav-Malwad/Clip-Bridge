using ClipBridge.Protocol;
using ClipBridge.Windows;
using Xunit;

namespace ClipBridge.Windows.Tests;

public sealed class ClipboardCoordinatorTests
{
    [Fact]
    public async Task LocalClipboardTextIsSentOnce()
    {
        var clipboard = new FakeClipboard("hello");
        var sent = new List<string>();
        var coordinator = new ClipboardCoordinator(clipboard, text => { sent.Add(text); return Task.CompletedTask; });

        await coordinator.HandleLocalClipboardChangedAsync();
        await coordinator.HandleLocalClipboardChangedAsync();

        Assert.Equal(["hello"], sent);
    }

    [Fact]
    public async Task RemoteTextIsAppliedWithoutEcho()
    {
        var clipboard = new FakeClipboard(null);
        var sent = new List<string>();
        var coordinator = new ClipboardCoordinator(clipboard, text => { sent.Add(text); return Task.CompletedTask; });

        var applied = coordinator.ApplyRemoteText("from phone");
        await coordinator.HandleLocalClipboardChangedAsync();

        Assert.True(applied);
        Assert.Equal("from phone", clipboard.Text);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task EmptyAndOversizedClipboardTextAreNotSent()
    {
        var clipboard = new FakeClipboard("");
        var sent = new List<string>();
        var coordinator = new ClipboardCoordinator(clipboard, text => { sent.Add(text); return Task.CompletedTask; });

        await coordinator.HandleLocalClipboardChangedAsync();
        clipboard.Text = new string('x', ClipboardProtocol.MaxPayloadBytes + 1);
        await coordinator.HandleLocalClipboardChangedAsync();

        Assert.Empty(sent);
    }

    [Fact]
    public async Task PausedCoordinatorNeitherSendsNorApplies()
    {
        var clipboard = new FakeClipboard("local");
        var sent = new List<string>();
        var coordinator = new ClipboardCoordinator(clipboard, text => { sent.Add(text); return Task.CompletedTask; })
        {
            IsPaused = true
        };

        await coordinator.HandleLocalClipboardChangedAsync();
        var applied = coordinator.ApplyRemoteText("remote");

        Assert.Empty(sent);
        Assert.False(applied);
        Assert.Equal("local", clipboard.Text);
    }

    private sealed class FakeClipboard(string? text) : IClipboardService
    {
        public string? Text { get; set; } = text;
        public string? GetText() => Text;
        public void SetText(string text) => Text = text;
    }
}
