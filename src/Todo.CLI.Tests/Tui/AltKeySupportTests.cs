using Todo.Tui.Input;
using Xunit;

namespace Todo.CLI.Tests.Tui;

public class AltKeySupportTests
{
    [Theory]
    [InlineData(AltKeyMode.On, true, "iTerm.app", AltKeyStatus.Confirmed)]
    [InlineData(AltKeyMode.Off, false, null, AltKeyStatus.Unconfirmed)]
    [InlineData(AltKeyMode.Auto, false, null, AltKeyStatus.Confirmed)]
    [InlineData(AltKeyMode.Auto, true, "iTerm.app", AltKeyStatus.Unconfirmed)]
    [InlineData(AltKeyMode.Auto, true, "kitty", AltKeyStatus.Unconfirmed)]
    [InlineData(AltKeyMode.Auto, true, null, AltKeyStatus.Unconfirmed)]
    public void Evaluate_DoesNotProbeTerminalAppUnlessNeeded(AltKeyMode mode, bool mac, string? term, AltKeyStatus expected)
    {
        var status = AltKeySupport.Evaluate(mode, mac, term, () => throw new Xunit.Sdk.XunitException("must not probe"));

        Assert.Equal(expected, status);
    }

    [Theory]
    [InlineData(true, AltKeyStatus.Confirmed)]
    [InlineData(false, AltKeyStatus.Unconfirmed)]
    [InlineData(null, AltKeyStatus.Unconfirmed)]
    public void Evaluate_AppleTerminal_UsesOptionAsMetaSetting(bool? setting, AltKeyStatus expected)
    {
        var status = AltKeySupport.Evaluate(AltKeyMode.Auto, true, "Apple_Terminal", () => setting);

        Assert.Equal(expected, status);
    }
}
