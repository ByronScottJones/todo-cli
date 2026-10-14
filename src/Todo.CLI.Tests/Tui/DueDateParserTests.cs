using System;
using Todo.Core;
using Xunit;

namespace Todo.CLI.Tests.Tui;

public class DueDateParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("garbage")]
    [InlineData("2026-02-30")]
    [InlineData("13-01")]
    public void Parse_BlankOrInvalid_ReturnsNull(string? text)
    {
        Assert.Null(DueDateParser.Parse(text));
    }

    [Fact]
    public void Parse_FullDate_ReturnsThatDate()
    {
        Assert.Equal(new DateTime(2027, 3, 9), DueDateParser.Parse("2027-03-09"));
    }

    [Fact]
    public void Parse_MonthDay_UsesCurrentYear()
    {
        Assert.Equal(new DateTime(DateTime.Now.Year, 3, 9), DueDateParser.Parse("03-09"));
    }
}
