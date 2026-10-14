using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Todo.Tui.Views;

/// <summary>Single-line text prompt, used for searching. Empty text is valid and clears the filter.</summary>
public sealed class TextPromptDialog : OkCancelDialog
{
    private readonly TextField _field;

    public TextPromptDialog(string title, string label, string initial, TuiTheme theme) : base(title, theme)
    {
        Width = Dim.Auto(DimAutoStyle.Content, minimumContentDim: Dim.Absolute(50), maximumContentDim: Dim.Percent(90));
        Height = 8;
        Add(new Label { Text = label, X = 1, Y = 1 });
        _field = new TextField { X = 1, Y = 2, Width = Dim.Fill(1), Value = initial };
        Add(_field);
        FocusOnOpen(_field);
    }

    public string Query => _field.Value ?? string.Empty;
}
