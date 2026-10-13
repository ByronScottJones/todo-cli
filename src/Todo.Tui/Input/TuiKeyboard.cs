using Terminal.Gui.App;
using Terminal.Gui.Input;

namespace Todo.Tui.Input;

public static class TuiKeyboard
{
    /// <summary>
    /// Removes Terminal.Gui's default application quit keys (Esc, Ctrl+Q). Esc must only close a popup, cancel an
    /// action or back out of a screen, and Ctrl+Q is reserved for terminal flow control. Exit is F3 / Alt+X.
    /// </summary>
    public static void Configure(IApplication app) => app.Keyboard.KeyBindings.Clear(Command.Quit);
}
