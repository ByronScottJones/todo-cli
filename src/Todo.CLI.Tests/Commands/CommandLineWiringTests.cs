using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Todo.CLI.Commands;
using Todo.CLI.UI;
using Todo.Core.Model;
using Todo.Core.Repository;
using Xunit;

namespace Todo.CLI.Tests.Commands;

/// <summary>
/// Covers FR-1 to FR-6 at the command-line layer: that the real command tree parses arguments, options, aliases,
/// and help as documented, and that parsed values reach the handlers. Handler logic itself is covered by the
/// per-command test classes. Only mocked repositories are ever invoked here, never a real account.
/// </summary>
public class CommandLineWiringTests
{
    private readonly Mock<ITodoListRepository> _lists = new();
    private readonly Mock<ITodoItemRepository> _items = new();
    private readonly RootCommand _root;

    public CommandLineWiringTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_lists.Object);
        services.AddSingleton(_items.Object);
        services.AddSingleton(Mock.Of<IUserInteraction>());
        _root = new TodoCommand(services.BuildServiceProvider());
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("-H")]
    [InlineData("--help")]
    [InlineData("--HELP")]
    [InlineData("-?")]
    public void HelpAliases_AllRequestHelp(string alias)
    {
        var result = _root.Parse([alias]);

        Assert.Empty(result.Errors);
        Assert.IsType<HelpAction>(result.Action);
    }

    [Theory]
    [InlineData("add", "item", "-H")]
    [InlineData("list", "--HELP")]
    [InlineData("remove", "list", "-?")]
    public void HelpAliases_WorkOnSubcommands(params string[] args)
    {
        var result = _root.Parse(args);

        Assert.Empty(result.Errors);
        Assert.IsType<HelpAction>(result.Action);
    }

    [Fact]
    public void Version_IsRecognized()
    {
        Assert.Empty(_root.Parse(["--version"]).Errors);
    }

    [Fact]
    public void BareTodo_ParsesWithoutErrors()
    {
        var result = _root.Parse([]);

        Assert.Empty(result.Errors);
        Assert.Empty(result.Tokens);
    }

    [Fact]
    public void TopLevelCommands_AreTheDocumentedSet()
    {
        var names = _root.Subcommands.Select(c => c.Name).ToArray();

        Assert.Equal(["add", "list", "complete", "remove", "tui"], names);
    }

    [Fact]
    public async Task AddItem_ParsedValuesReachTheHandler()
    {
        var list = new TodoList { Id = "L1", Name = "Work" };
        _lists.Setup(r => r.GetByNameAsync("Work")).ReturnsAsync(list);

        var exit = await Invoke("add", "item", "Buy milk", "--list", "Work", "--star", "--due-date", "2026-12-25");

        Assert.Equal(0, exit);
        _items.Verify(r => r.AddAsync(It.Is<TodoItem>(i =>
            i.Subject == "Buy milk" &&
            i.ListId == "L1" &&
            i.IsImportant &&
            i.DueDate == new DateTime(2026, 12, 25))), Times.Once);
    }

    [Fact]
    public async Task AddList_ParsedNameReachesTheHandler()
    {
        var exit = await Invoke("add", "list", "Errands");

        Assert.Equal(0, exit);
        _lists.Verify(r => r.AddAsync(It.Is<TodoList>(l => l.Name == "Errands")), Times.Once);
    }

    [Fact]
    public async Task AddItem_MissingSubject_IsAParseErrorWithNonZeroExit()
    {
        var parse = _root.Parse(["add", "item"]);
        var exit = await Invoke("add", "item");

        Assert.NotEmpty(parse.Errors);
        Assert.NotEqual(0, exit);
        _items.Verify(r => r.AddAsync(It.IsAny<TodoItem>()), Times.Never);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("add", "list", "Name", "Extra")]
    [InlineData("list", "-o", "xml")]
    [InlineData("list", "--older-than", "notadate")]
    [InlineData("add", "item", "x", "--star", "maybe")]
    public void InvalidInput_IsAParseError(params string[] args)
    {
        Assert.NotEmpty(_root.Parse(args).Errors);
    }

    [Theory]
    [InlineData("-o", "json")]
    [InlineData("--output", "json")]
    [InlineData("--output", "Json")]
    public void OutputOption_ParsesJsonUnderBothAliasesOnTheRootAndList(string alias, string value)
    {
        var onList = _root.Parse(["list", alias, value]);
        var onRoot = _root.Parse([alias, value, "list"]);

        Assert.Empty(onList.Errors);
        Assert.Equal(OutputFormat.Json, onList.GetValue(TodoCommand.OutputFormat));
        Assert.Empty(onRoot.Errors);
        Assert.Equal(OutputFormat.Json, onRoot.GetValue(TodoCommand.OutputFormat));
    }

    [Fact]
    public void OutputOption_DefaultsToInteractive()
    {
        Assert.Equal(OutputFormat.Interactive, _root.Parse(["list"]).GetValue(TodoCommand.OutputFormat));
    }

    [Fact]
    public void List_ParsesFiltersAndListName()
    {
        var result = _root.Parse(["list", "Work", "-a", "--no-status", "--older-than", "2026-01-31"]);

        Assert.Empty(result.Errors);
        Assert.Equal("Work", result.GetValue<string>("list-name"));
        Assert.True(result.GetValue<bool>("--all"));
        Assert.True(result.GetValue<bool>("--no-status"));
        Assert.Equal(new DateTime(2026, 1, 31), result.GetValue<DateTime?>("--older-than"));
    }

    [Fact]
    public void Complete_ParsesSeveralIdsAndFilters()
    {
        var result = _root.Parse(["complete", "id1", "id2", "-l", "Work", "--all", "--older-than", "2026-02-01"]);

        Assert.Empty(result.Errors);
        Assert.Equal(["id1", "id2"], result.GetValue<string[]>("id"));
        Assert.Equal("Work", result.GetValue<string>("--list"));
        Assert.True(result.GetValue<bool>("--all"));
        Assert.Equal(new DateTime(2026, 2, 1), result.GetValue<DateTime?>("--older-than"));
    }

    [Fact]
    public void RemoveItem_ParsesIdsAndFlagsUnderShortAliases()
    {
        var result = _root.Parse(["remove", "item", "a", "b", "-c", "-a", "-l", "Inbox"]);

        Assert.Empty(result.Errors);
        Assert.Equal(["a", "b"], result.GetValue<string[]>("id"));
        Assert.True(result.GetValue<bool>("--completed"));
        Assert.True(result.GetValue<bool>("--all"));
        Assert.Equal("Inbox", result.GetValue<string>("--list"));
    }

    [Fact]
    public void RemoveList_ParsesTheListName()
    {
        var result = _root.Parse(["remove", "list", "Old Stuff"]);

        Assert.Empty(result.Errors);
        Assert.Equal("Old Stuff", result.GetValue<string>("name"));
    }

    [Fact]
    public void Directives_ParseAndEnvironmentOnesAreAccepted()
    {
        Assert.Empty(_root.Parse(["[diagram]", "add", "item", "x"]).Errors);
        Assert.Empty(_root.Parse(["[env:TODO_TEST=1]", "list"]).Errors);
    }

    private async Task<int> Invoke(params string[] args)
    {
        // Keep parse-error and help text out of the test output.
        var configuration = new InvocationConfiguration { Output = new StringWriter(), Error = new StringWriter() };
        return await _root.Parse(args).InvokeAsync(configuration);
    }
}
