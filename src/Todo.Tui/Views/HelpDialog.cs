using System.Collections.ObjectModel;
using System.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Todo.Tui.Input;

namespace Todo.Tui.Views;

/// <summary>Scrollable help generated from the live key map, so it never drifts from the real bindings.</summary>
public sealed class HelpDialog : OkCancelDialog
{
    public HelpDialog(AltKeyStatus altStatus, TuiTheme theme) : base("Help - Todo", theme)
    {
        Remove(CancelButton);
        Width = Dim.Percent(85);
        Height = Dim.Percent(85);

        // A read-only ListView scrolls with the arrow keys, PgUp/PgDn and Home/End.
        var lines = BuildText(altStatus).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var view = new ListView { X = 1, Y = 0, Width = Dim.Fill(1), Height = Dim.Fill(2) };
        view.SetSource(new ObservableCollection<string>(lines));
        Add(view);
        FocusOnOpen(view);
    }

    /// <summary>Help content. Lines stay within <see cref="MaxLineLength"/> so they fit a 60-column terminal.</summary>
    public const int MaxLineLength = 54;

    public static string BuildText(AltKeyStatus altStatus)
    {
        var text = new StringBuilder();
        text.AppendLine("TODO ITEMS (Microsoft To Do)");
        text.AppendLine();
        text.AppendLine("Everything works from the keyboard. Arrow keys");
        text.AppendLine("move; Tab / Shift+Tab move between controls.");
        text.AppendLine("Enter opens the selected item; Esc cancels.");
        text.AppendLine();
        text.AppendLine("COMMANDS (Alt key, or Ctrl alternative)");
        foreach (var command in Enum.GetValues<TuiCommand>())
        {
            text.AppendLine($"  {CommandKeys.AltText(command),-6} / {CommandKeys.CtrlText(command),-6}  {Describe(command)}");
        }

        text.AppendLine();
        text.AppendLine("GLOBAL KEYS");
        text.AppendLine("  F1          This help");
        text.AppendLine("  F3, Alt+X   Exit (F3 closes an open dialog)");
        text.AppendLine("  F5          Reload from Microsoft To Do");
        text.AppendLine("  F10         Menu bar (Alt+T/F/E/V/H: menus)");
        text.AppendLine("  F7 / F8     Page up / down (PgUp, PgDn too)");
        text.AppendLine("  Space       Complete / reopen item");
        text.AppendLine("  Delete      Remove item");
        text.AppendLine("  ?           Search by subject");
        text.AppendLine("  Ctrl+C      Copy subject (copy in fields)");
        text.AppendLine("  Ctrl+X / V  Cut / paste in text fields");
        text.AppendLine();
        text.AppendLine("ALT KEYS");
        if (altStatus == AltKeyStatus.Confirmed)
        {
            text.AppendLine("  Alt shortcuts are active.");
            text.AppendLine("  The Ctrl alternatives also work.");
        }
        else
        {
            text.AppendLine("  Alt could not be confirmed (on macOS, Option");
            text.AppendLine("  often is not sent as Alt), so Ctrl shortcuts");
            text.AppendLine("  are shown. Enable \"Use Option as Meta key\" in");
            text.AppendLine("  your terminal profile, or set Tui:AltKeys to");
            text.AppendLine("  \"on\" in appsettings.json.");
        }

        text.AppendLine();
        text.AppendLine("ITEM FORM");
        text.AppendLine("  Subject is required.");
        text.AppendLine("  Due date is optional: yyyy-MM-dd or MM-dd;");
        text.AppendLine("  blank clears it.");
        text.AppendLine("  [ OK ] stays disabled until the form is valid.");
        text.AppendLine("  Items cannot be moved between lists.");
        return text.ToString();
    }

    private static string Describe(TuiCommand command) => command switch
    {
        TuiCommand.New => "Add an item",
        TuiCommand.Edit => "Modify the item",
        TuiCommand.ToggleComplete => "Complete / reopen",
        TuiCommand.ToggleImportant => "Mark / unmark important",
        TuiCommand.Remove => "Remove the item",
        TuiCommand.Lists => "Choose the list shown",
        TuiCommand.Search => "Search by subject",
        TuiCommand.ShowCompleted => "Show / hide completed",
        _ => command.ToString()
    };
}
