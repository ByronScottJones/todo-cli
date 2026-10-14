using System;
using System.Linq;
using Todo.Tui.Input;
using Xunit;

namespace Todo.CLI.Tests.Tui;

public class CommandKeysTests
{
    private static readonly TuiCommand[] All = Enum.GetValues<TuiCommand>();

    [Fact]
    public void AltBindings_AreUnique()
    {
        var keys = All.Select(c => CommandKeys.AltText(c)).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void CtrlBindings_AreUnique()
    {
        var keys = All.Select(c => CommandKeys.CtrlText(c)).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void AltBindings_DoNotCollideWithMenuMnemonicsOrExit()
    {
        // Menus: Todo, File, Edit, View, Help; Alt+X exits.
        var reserved = new[] { "Alt+T", "Alt+F", "Alt+E", "Alt+V", "Alt+H", "Alt+X" };

        Assert.DoesNotContain(All, c => reserved.Contains(CommandKeys.AltText(c)));
    }

    [Fact]
    public void CtrlBindings_DoNotCollideWithTerminalControlOrCuaEditKeys()
    {
        // Q: flow control. C/X/V/Z: CUA edit keys. I/M/H/J: Tab, Enter, Backspace, newline. D: text-field delete.
        var reserved = new[] { "Ctrl+Q", "Ctrl+S", "Ctrl+C", "Ctrl+X", "Ctrl+V", "Ctrl+Z", "Ctrl+I", "Ctrl+M", "Ctrl+H", "Ctrl+J", "Ctrl+D" };

        Assert.DoesNotContain(All, c => reserved.Contains(CommandKeys.CtrlText(c)));
    }

    [Fact]
    public void Text_FollowsAltStatus()
    {
        Assert.Equal("Alt+N", CommandKeys.Text(TuiCommand.New, AltKeyStatus.Confirmed));
        Assert.Equal("Ctrl+N", CommandKeys.Text(TuiCommand.New, AltKeyStatus.Unconfirmed));
    }

    [Fact]
    public void Keys_CarryTheExpectedModifier()
    {
        Assert.True(CommandKeys.AltKey(TuiCommand.New).IsAlt);
        Assert.True(CommandKeys.CtrlKey(TuiCommand.New).IsCtrl);
    }
}
