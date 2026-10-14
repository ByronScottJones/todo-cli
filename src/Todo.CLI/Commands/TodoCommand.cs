using System;
using System.CommandLine;
using System.Threading.Tasks;
using Todo.CLI.Handlers;
using Todo.CLI.UI;
using Todo.Tui;

namespace Todo.CLI.Commands;

public class TodoCommand : RootCommand
{
    public static readonly Option<OutputFormat> OutputFormat = new(
        aliases: ["-o", "--output"],
        description: "Specifies the output format.",
        getDefaultValue: () => UI.OutputFormat.Interactive);

    public TodoCommand(IServiceProvider serviceProvider)
    {
        // Add static parameters
        Description = "A CLI to manage Microsoft to do items.";

        // Add output format option
        Add(OutputFormat);

        // Add subcommands
        Add(new AddCommand(serviceProvider));
        Add(new ListCommand(serviceProvider));
        Add(new CompleteCommand(serviceProvider));
        Add(new RemoveCommand(serviceProvider));
        Add(new TuiCommand(serviceProvider));

        // Bare `todo` opens the TUI in an interactive terminal; otherwise it prints help, as before.
        this.SetHandler(context => RunWithoutSubcommandAsync(serviceProvider, context));
    }

    private Task RunWithoutSubcommandAsync(IServiceProvider serviceProvider, System.CommandLine.Invocation.InvocationContext context)
    {
        var bareInvocation = context.ParseResult.Tokens.Count == 0;
        if (bareInvocation && TuiHost.IsInteractive)
            return TuiCommand.RunTuiAsync(serviceProvider, context);

        context.HelpBuilder.Write(new System.CommandLine.Help.HelpContext(context.HelpBuilder, this, Console.Out));
        return Task.CompletedTask;
    }
}
