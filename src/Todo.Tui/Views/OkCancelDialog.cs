using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Todo.Tui.Views;

/// <summary>
/// CUA dialog frame: [ OK ] commits on Enter, [ Cancel ] discards on Esc or F3, and focus stays trapped
/// until one of them is chosen.
/// </summary>
public abstract class OkCancelDialog : Dialog
{
    protected OkCancelDialog(string title, TuiTheme theme)
    {
        Title = title;
        SetScheme(theme.Dialog);

        OkButton = new Button { Text = "_OK", IsDefault = true };
        CancelButton = new Button { Text = "_Cancel" };
        OkButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            if (!CanConfirm())
                return;
            Confirmed = true;
            RequestStop();
        };
        CancelButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            RequestStop();
        };

        AddButton(OkButton);
        AddButton(CancelButton);

        // Dialog treats the last button as the default; Enter must commit, so point it at [ OK ].
        DefaultAcceptView = OkButton;
        OkButton.IsDefault = true;
        CancelButton.IsDefault = false;

        KeyDown += (_, key) =>
        {
            if (key == Key.Esc || key == Key.F3)
            {
                key.Handled = true;
                RequestStop();
            }
        };
    }

    public bool Confirmed { get; private set; }

    /// <summary>
    /// Sets which control receives focus when the dialog opens (the first entry field, not a button).
    /// </summary>
    protected void FocusOnOpen(View view) =>
        IsRunningChanged += (_, args) =>
        {
            // The dialog frame focuses its last button when it starts running; override that.
            if (args.Value)
                view.SetFocus();
        };

    protected Button OkButton { get; }

    protected Button CancelButton { get; }

    /// <summary>
    /// Stops Enter in an entry field from closing the dialog while the input is invalid. Without this a disabled
    /// [ OK ] lets the dialog's built-in accept run, closing the form and hiding the inline error.
    /// </summary>
    protected void GuardEnter(View field) =>
        field.Accepting += (_, e) =>
        {
            if (!CanConfirm())
                e.Handled = true;
        };

    /// <summary>Return false to keep the dialog open, e.g. while the form is invalid.</summary>
    protected virtual bool CanConfirm() => true;
}
