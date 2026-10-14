using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Todo.Tui.Views;

/// <summary>Confirmation or error message with [ OK ] and optionally [ Cancel ].</summary>
public sealed class MessageDialog : OkCancelDialog
{
    public MessageDialog(string title, string message, TuiTheme theme, bool showCancel, bool isError = false)
        : base(title, theme)
    {
        if (isError)
            SetScheme(theme.DialogError);

        Width = Dim.Percent(60);
        Height = Dim.Auto(DimAutoStyle.Content, minimumContentDim: Dim.Absolute(7));
        MinimumWidth();
        Add(new Label { Text = message, X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Auto() });

        if (!showCancel)
            Remove(CancelButton);

        FocusOnOpen(OkButton);
    }

    private void MinimumWidth()
    {
        Width = Dim.Auto(DimAutoStyle.Content, minimumContentDim: Dim.Absolute(44), maximumContentDim: Dim.Percent(90));
    }
}
