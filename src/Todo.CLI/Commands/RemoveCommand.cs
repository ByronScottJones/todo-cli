using System;
using System.CommandLine;
using Todo.CLI.Handlers;
using Todo.CLI.UI;

namespace Todo.CLI.Commands;

public class RemoveCommand : Command
{
    public RemoveCommand(IServiceProvider serviceProvider) : base("remove", "Deletes to do items.")
    {
        Add(new RemoveItemCommand(serviceProvider));
        Add(new RemoveListCommand(serviceProvider));
    }

    internal class RemoveItemCommand : Command
    {

        private static readonly Argument<string[]> IdArg =
            new("id")
            {
                Description = "The ID of the todo item(s) to remove.",
                Arity = ArgumentArity.ZeroOrMore
            };
        private static readonly Option<string> ListOpt = new("--list", "-l") { Description = "The name of the list to remove the item from." };
        private static readonly Option<DateTime?> OlderThanOpt = new("--older-than") { Description = "Only items completed before this date." };
        private static readonly Option<bool> AllOpt = new("--all", "-a") { Description = "Remove all items fitting the filter. You will be prompted before removal." };
        private static readonly Option<bool> CompletedOpt = new("--completed", "-c") { Description = "Remove only completed items." };

        public RemoveItemCommand(IServiceProvider serviceProvider) : base("item", "Deletes to do items.")
        {
            Add(IdArg);
            Add(ListOpt);
            Add(OlderThanOpt);
            Add(AllOpt);
            Add(CompletedOpt);

            var handler = RemoveCommandHandler.Item.Create(serviceProvider);
            SetAction(parseResult => handler(
                parseResult.GetValue(IdArg) ?? [],
                parseResult.GetValue(ListOpt),
                parseResult.GetValue(OlderThanOpt),
                parseResult.GetValue(AllOpt),
                parseResult.GetValue(CompletedOpt)));
        }
    }

    internal class RemoveListCommand : Command
    {

        private static readonly Argument<string> NameArg = new("name") { Description = "The name of the list to remove." };
        public RemoveListCommand(IServiceProvider serviceProvider) : base("list", "Deletes to do list(s).")
        {
            Add(NameArg);

            var handler = RemoveCommandHandler.List.Create(serviceProvider);
            SetAction(async parseResult =>
            {
                await handler(parseResult.GetRequiredValue(NameArg));
                return 0;
            });
        }
    }
}
