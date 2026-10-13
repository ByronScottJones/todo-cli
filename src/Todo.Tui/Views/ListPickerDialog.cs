using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Todo.Tui.Views;

/// <summary>Pick one entry from a list of names; Enter or [ OK ] confirms.</summary>
public sealed class ListPickerDialog : OkCancelDialog
{
    private readonly ListView _list;

    public ListPickerDialog(string title, IReadOnlyList<string> names, int selectedIndex, TuiTheme theme)
        : base(title, theme)
    {
        Width = Dim.Auto(DimAutoStyle.Content, minimumContentDim: Dim.Absolute(40), maximumContentDim: Dim.Percent(90));
        Height = Dim.Auto(DimAutoStyle.Content, minimumContentDim: Dim.Absolute(10), maximumContentDim: Dim.Percent(90));

        _list = new ListView { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill(2) };
        _list.SetSource(new ObservableCollection<string>(names));
        if (names.Count > 0)
            _list.SelectedItem = Math.Clamp(selectedIndex, 0, names.Count - 1);
        // Enter on a row confirms the dialog.
        _list.Accepting += (_, e) =>
        {
            e.Handled = true;
            OkButton.InvokeCommand(Terminal.Gui.Input.Command.Accept);
        };
        Add(_list);
        FocusOnOpen(_list);
    }

    public int SelectedIndex => _list.SelectedItem ?? 0;
}
