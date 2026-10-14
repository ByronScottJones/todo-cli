using Todo.Tui.Model;
using Xunit;

namespace Todo.CLI.Tests.Tui;

public class ScrollWindowTests
{
    [Theory]
    [InlineData(0, 0, 10, 0, 0)]
    [InlineData(5, 0, 10, 0, 0)]
    [InlineData(30, 0, 10, 0, 20)]
    [InlineData(30, 10, 10, 10, 10)]
    [InlineData(30, 20, 10, 20, 0)]
    [InlineData(30, 99, 10, 29, 0)]
    [InlineData(30, -5, 10, 0, 20)]
    [InlineData(30, 5, 0, 0, 0)]
    public void Compute_ReportsItemsOutsideTheWindow(int total, int top, int visible, int above, int below)
    {
        var window = ScrollWindow.Compute(total, top, visible);

        Assert.Equal(above, window.Above);
        Assert.Equal(below, window.Below);
    }

    [Theory]
    [InlineData(0, 0, "")]
    [InlineData(3, 0, "3 more above")]
    [InlineData(0, 4, "4 more below")]
    [InlineData(3, 4, "3 more above, 4 more below")]
    public void Describe_FormatsIndicator(int above, int below, string expected)
    {
        Assert.Equal(expected, new ScrollWindow(above, below).Describe());
    }
}
