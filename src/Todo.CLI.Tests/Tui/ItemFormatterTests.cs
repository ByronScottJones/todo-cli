using System;
using Terminal.Gui.Text;
using Todo.Core.Model;
using Todo.Tui.Model;
using Xunit;

namespace Todo.CLI.Tests.Tui;

public class ItemFormatterTests
{
    private static readonly DateTime Today = new(2026, 11, 1);

    [Theory]
    [InlineData(40)]
    [InlineData(80)]
    [InlineData(20)]
    public void FormatRow_AlwaysFillsExactlyTheRequestedWidth(int width)
    {
        var item = new TodoItem { Subject = "A fairly long subject line that will need truncating", DueDate = new DateTime(2026, 11, 5) };

        var row = ItemFormatter.FormatRow(item, "Inbox", width, Today, Glyphs.Unicode);

        Assert.Equal(width, row.GetColumns());
    }

    [Fact]
    public void FormatRow_WideCharacters_StillFitsTheWidth()
    {
        var item = new TodoItem { Subject = "買い物リスト買い物リスト買い物リスト" };

        var row = ItemFormatter.FormatRow(item, null, 30, Today, Glyphs.Unicode);

        Assert.Equal(30, row.GetColumns());
    }

    [Fact]
    public void FormatRow_ShowsCompletionAndImportanceAsText()
    {
        var open = ItemFormatter.FormatRow(new TodoItem { Subject = "x" }, null, 30, Today, Glyphs.Unicode);
        var done = ItemFormatter.FormatRow(new TodoItem { Subject = "x", IsCompleted = true, IsImportant = true }, null, 30, Today, Glyphs.Unicode);

        Assert.StartsWith("☐", open);
        Assert.StartsWith("☑ ★", done);
    }

    [Fact]
    public void FormatRow_AsciiGlyphs_UsesNoNonAsciiCharacters()
    {
        var row = ItemFormatter.FormatRow(new TodoItem { Subject = "x", IsCompleted = true, IsImportant = true }, null, 30, Today, Glyphs.Ascii);

        Assert.All(row, c => Assert.True(c < 128));
        Assert.StartsWith("[x] *", row);
    }

    [Fact]
    public void BuildSuffix_OverdueOpenItem_IsMarkedWithText()
    {
        var item = new TodoItem { Subject = "x", DueDate = new DateTime(2026, 10, 31) };

        Assert.Contains("! due 10-31", ItemFormatter.BuildSuffix(item, null, Today, Glyphs.Unicode));
    }

    [Fact]
    public void BuildSuffix_OverdueButCompleted_IsNotMarkedOverdue()
    {
        var item = new TodoItem { Subject = "x", IsCompleted = true, DueDate = new DateTime(2026, 10, 31) };

        Assert.DoesNotContain("!", ItemFormatter.BuildSuffix(item, null, Today, Glyphs.Unicode));
    }

    [Fact]
    public void BuildSuffix_DueTodayIsNotOverdue()
    {
        var item = new TodoItem { Subject = "x", DueDate = Today };

        var suffix = ItemFormatter.BuildSuffix(item, "Inbox", Today, Glyphs.Unicode);

        Assert.Equal("  due 11-01  [Inbox]", suffix);
    }

    [Fact]
    public void FormatRow_NullSubject_DoesNotThrow()
    {
        var row = ItemFormatter.FormatRow(new TodoItem { Subject = null }, null, 20, Today, Glyphs.Unicode);

        Assert.Equal(20, row.GetColumns());
    }

    [Theory]
    [InlineData("short", 10, "short")]
    [InlineData("exactly10!", 10, "exactly10!")]
    [InlineData("this is too long", 8, "this is…")]
    [InlineData("anything", 1, "…")]
    [InlineData("anything", 0, "")]
    public void FitToWidth_TruncatesWithEllipsis(string text, int width, string expected)
    {
        Assert.Equal(expected, ItemFormatter.FitToWidth(text, width, "…"));
    }

    [Fact]
    public void FormatRow_NarrowWidth_DropsListNameBeforeTheDueDate()
    {
        var item = new TodoItem { Subject = "Pay the rent", DueDate = new DateTime(2026, 11, 5) };

        var row = ItemFormatter.FormatRow(item, "Household", 34, Today, Glyphs.Unicode);

        Assert.Contains("due 11-05", row);
        Assert.DoesNotContain("Household", row);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void FormatRow_TinyWidth_NeverOverflows(int width)
    {
        var item = new TodoItem { Subject = "Pay the rent", IsImportant = true, DueDate = new DateTime(2026, 11, 5) };

        var row = ItemFormatter.FormatRow(item, "Household", width, Today, Glyphs.Unicode);

        Assert.True(row.GetColumns() <= width);
    }
}
