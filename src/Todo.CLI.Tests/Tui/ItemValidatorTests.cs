using Todo.Tui.Model;
using Xunit;

namespace Todo.CLI.Tests.Tui;

public class ItemValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankSubject_ReturnsSubjectRequired(string? subject)
    {
        var result = ItemValidator.Validate(subject, null);

        Assert.False(result.IsValid);
        Assert.Equal(ItemValidator.SubjectRequired, result.SubjectError);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("2026-12-25")]
    [InlineData("12-25")]
    public void Validate_BlankOrValidDueDate_IsValid(string? due)
    {
        var result = ItemValidator.Validate("Buy milk", due);

        Assert.True(result.IsValid);
        Assert.Null(result.DueDateError);
    }

    [Theory]
    [InlineData("tomorrow")]
    [InlineData("2026-13-01")]
    [InlineData("25-12")]
    [InlineData("12/25")]
    [InlineData("2026-02-30")]
    public void Validate_InvalidDueDate_ReturnsDueDateError(string due)
    {
        var result = ItemValidator.Validate("Buy milk", due);

        Assert.False(result.IsValid);
        Assert.Equal(ItemValidator.DueDateInvalid, result.DueDateError);
        Assert.Null(result.SubjectError);
    }

    [Fact]
    public void Validate_BothInvalid_ReportsBothErrors()
    {
        var result = ItemValidator.Validate("", "nope");

        Assert.NotNull(result.SubjectError);
        Assert.NotNull(result.DueDateError);
    }
}
