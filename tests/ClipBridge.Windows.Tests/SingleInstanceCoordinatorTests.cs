using ClipBridge.Windows;
using Xunit;

namespace ClipBridge.Windows.Tests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public async Task SecondInstanceSignalsPrimaryInstance()
    {
        var name = $"ClipBridge.Tests.{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(name);
        Assert.True(primary.IsPrimary);
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.StartListening(() => activated.TrySetResult());

        await Task.Run(() =>
        {
            using var secondary = new SingleInstanceCoordinator(name);
            Assert.False(secondary.IsPrimary);
            secondary.SignalPrimary();
        }, TestContext.Current.CancellationToken);

        await activated.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }
}
