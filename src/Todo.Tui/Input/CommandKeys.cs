using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace Todo.Tui.Input;

/// <summary>Application commands that have a custom shortcut.</summary>
public enum TuiCommand
{
    New,
    Edit,
    ToggleComplete,
    ToggleImportant,
    Remove,
    Lists,
    Search,
    ShowCompleted,
    Theme
}

/// <summary>
/// Shortcut map for custom commands. Alt+letter is the primary binding; the Ctrl alternatives are advertised
/// instead when Alt cannot be confirmed. Letters avoid the menu mnemonics (T, F, E, V, H) so Alt+menu keeps working.
/// Both bindings are always registered so either set works regardless of what is shown.
/// </summary>
public static class CommandKeys
{
    private sealed record Binding(char Letter, char CtrlLetter, string Label);

    private static readonly Dictionary<TuiCommand, Binding> Bindings = new()
    {
        [TuiCommand.New] = new('N', 'N', "New"),
        [TuiCommand.Edit] = new('M', 'E', "Modify"),
        [TuiCommand.ToggleComplete] = new('C', 'T', "Complete"),
        [TuiCommand.ToggleImportant] = new('I', 'P', "Important"),
        [TuiCommand.Remove] = new('R', 'R', "Remove"),
        [TuiCommand.Lists] = new('L', 'L', "Lists"),
        [TuiCommand.Search] = new('S', 'F', "Search"),
        [TuiCommand.ShowCompleted] = new('A', 'A', "All/Open"),
        [TuiCommand.Theme] = new('O', 'O', "Theme")
    };

    public static string Label(TuiCommand command) => Bindings[command].Label;

    public static Key AltKey(TuiCommand command) => ToKey(Bindings[command].Letter).WithAlt;

    public static Key CtrlKey(TuiCommand command) => ToKey(Bindings[command].CtrlLetter).WithCtrl;

    public static string AltText(TuiCommand command) => $"Alt+{Bindings[command].Letter}";

    public static string CtrlText(TuiCommand command) => $"Ctrl+{Bindings[command].CtrlLetter}";

    public static string Text(TuiCommand command, AltKeyStatus status) =>
        status == AltKeyStatus.Confirmed ? AltText(command) : CtrlText(command);

    private static Key ToKey(char letter) => new((KeyCode)letter);
}
