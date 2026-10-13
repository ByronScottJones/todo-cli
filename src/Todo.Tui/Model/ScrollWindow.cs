namespace Todo.Tui.Model;

/// <summary>How many items lie outside the visible part of a scrolling list.</summary>
public readonly record struct ScrollWindow(int Above, int Below)
{
    public static ScrollWindow Compute(int total, int top, int visibleRows)
    {
        if (total <= 0 || visibleRows <= 0)
            return new ScrollWindow(0, 0);

        var clampedTop = Math.Clamp(top, 0, total - 1);
        var below = Math.Max(0, total - (clampedTop + visibleRows));
        return new ScrollWindow(clampedTop, below);
    }

    public string Describe() => (Above, Below) switch
    {
        (0, 0) => string.Empty,
        (var a, 0) => $"{a} more above",
        (0, var b) => $"{b} more below",
        (var a, var b) => $"{a} more above, {b} more below"
    };
}
