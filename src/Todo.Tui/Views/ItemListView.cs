using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Todo.Tui.Views;

/// <summary>
/// List that lets the owner claim keys before the built-in type-ahead search sees them. Printable keys such as
/// <c>?</c> are otherwise consumed by the list and never reach window-level handlers.
/// </summary>
public sealed class ItemListView : ListView
{
    /// <summary>Return true from the handler to mark the key as consumed.</summary>
    public Func<Key, bool>? PreviewKey { get; set; }

    protected override bool OnKeyDown(Key key) => PreviewKey?.Invoke(key) == true || base.OnKeyDown(key);
}
