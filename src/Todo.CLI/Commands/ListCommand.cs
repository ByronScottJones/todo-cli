using System;
using System.CommandLine;
using System.Threading.Tasks;
using Todo.CLI.Handlers;
using Todo.CLI.UI;

namespace Todo.CLI.Commands;

public class ListCommand : Command
{
    private static readonly Option<bool> GetAllOption =
        new("--all", "-a") { Description = "Lists all to do items including the completed ones." };

    private static readonly Option<bool> NoStatusOption = new("--no-status")
    {
        Description = "Suppresses the bullet indicating whether the item is completed or not."
    };

    private static readonly Option<DateTime?> OlderThanOption =
        new("--older-than") { Description = "Only items completed before this date." };

    private static readonly Argument<string> ListNameArgument = new("list-name")
    {
        Description = "Only list tasks of this To-Do list.",
        Arity = ArgumentArity.ZeroOrOne
    };


    public ListCommand(IServiceProvider serviceProvider) : base("list")
    {
        Description = "Retrieves a list of the to do items across all To-Do lists.";

        Add(TodoCommand.OutputFormat);
        Add(GetAllOption);
        Add(NoStatusOption);
        Add(OlderThanOption);
        Add(ListNameArgument);

        var handler = ListCommandHandler.Create(serviceProvider);
        SetAction(async parseResult =>
        {
            await handler(
                parseResult.GetValue(GetAllOption),
                parseResult.GetValue(NoStatusOption),
                parseResult.GetValue(OlderThanOption),
                parseResult.GetValue(ListNameArgument));
            return 0;
        });
    }
}
