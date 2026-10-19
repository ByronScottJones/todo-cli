using System;
using System.CommandLine;
using Todo.CLI.Handlers;
using Todo.CLI.UI;

namespace Todo.CLI.Commands;

public class AddCommand : Command
{
    public AddCommand(IServiceProvider serviceProvider) : base("add", "Adds a to do item or list.")
    {
        Add(new AddListCommand(serviceProvider));
        Add(new AddItemCommand(serviceProvider));
    }

    internal class AddListCommand : Command
    {
        private static readonly Argument<string> NameArgument = new("name") { Description = "The name of the new to do list." };

        public AddListCommand(IServiceProvider serviceProvider) : base("list", "Adds a new to do list.")
        {
            Add(NameArgument);

            var handler = AddCommandHandler.List.Create(serviceProvider);
            SetAction(parseResult => handler(parseResult.GetRequiredValue(NameArgument)));
        }
    }

    internal class AddItemCommand : Command
    {
        private static readonly Argument<string> SubjectArgument = new("subject") { Description = "The subject of the new to do item." };
        private static readonly Option<string> ListOption = new("--list") { Description = "The list to add the to do item to." };
        private static readonly Option<bool> StarOption = new("--star") { Description = "Stars (marks as important) the new to do item." };
        private static readonly Option<string> DueDateOption = new("--due-date") { Description = "The due date (yyyy-MM-dd or MM-dd)." };

        public AddItemCommand(IServiceProvider serviceProvider) : base("item", "Adds a new to do item.")
        {
            Add(SubjectArgument);
            Add(ListOption);
            Add(StarOption);
            Add(DueDateOption);

            var handler = AddCommandHandler.Item.Create(serviceProvider);
            SetAction(parseResult => handler(
                parseResult.GetRequiredValue(SubjectArgument),
                parseResult.GetValue(ListOption),
                parseResult.GetValue(StarOption),
                parseResult.GetValue(DueDateOption)));
        }
    }
}
