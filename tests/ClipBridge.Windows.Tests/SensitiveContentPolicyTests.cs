using ClipBridge.Windows;
using Xunit;

namespace ClipBridge.Windows.Tests;

public sealed class SensitiveContentPolicyTests
{
    private readonly SensitiveContentPolicy _policy = new(["keepass", "1password"]);

    [Theory]
    [InlineData("-----BEGIN PRIVATE KEY-----\nabc")]
    [InlineData("seed phrase: alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu")]
    [InlineData("Your verification code is 481229")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("aB9xQ2mN7pL4vR8sT1wY6zC3dF5gH0jK")]
    public void LikelySecretsAreBlocked(string text) =>
        Assert.Equal(ContentDecision.BlockSensitive, _policy.Evaluate(text, null));

    [Theory]
    [InlineData("https://example.com/docs/clipbridge-security-design")]
    [InlineData("Please send the project notes to the team.")]
    [InlineData("1234 5678 9012 3456")]
    public void OrdinaryContentIsAllowed(string text) =>
        Assert.Equal(ContentDecision.Allow, _policy.Evaluate(text, null));

    [Fact]
    public void ExcludedApplicationIsBlockedWithoutInspectingItsText() =>
        Assert.Equal(ContentDecision.BlockExcludedApplication, _policy.Evaluate("hello", "KeePass"));
}
