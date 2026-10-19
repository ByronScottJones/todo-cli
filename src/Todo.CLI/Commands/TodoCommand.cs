using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.Threading.Tasks;
using Todo.CLI.UI;
using Todo.Tui;

namespace Todo.CLI.Commands;

public class TodoCommand : RootCommand
{
    public static readonly Option<OutputFormat> OutputFormat = new("--output", "-o")
    {
        Description = "Specifies the output format.",
        DefaultValueFactory = _ => UI.OutputFormat.Interactive
    };

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

        // The standard help option, but with the full set of help aliases.
        for (var i = Options.Count - 1; i >= 0; i--)
        {
            if (Options[i] is HelpOption)
                Options.RemoveAt(i);
        }
        Options.Add(new HelpOption("--help", "-h", "-H", "--HELP", "-?"));

        // Directives are opt-in in System.CommandLine 3: keep the environment-variable one, and `[diagram]` replaces
        // the earlier `[parse]`. `[suggest]` is registered by RootCommand.
        Directives.Add(new EnvironmentVariablesDirective());
        Directives.Add(new DiagramDirective());

        // Bare `todo` opens the TUI in an interactive terminal; otherwise it prints help, as before.
        SetAction(parseResult => RunWithoutSubcommandAsync(serviceProvider, parseResult));
    }

    private Task<int> RunWithoutSubcommandAsync(IServiceProvider serviceProvider, ParseResult parseResult)
    {
        var bareInvocation = parseResult.Tokens.Count == 0;
        if (bareInvocation && TuiHost.IsInteractive)
            return TuiCommand.RunTuiAsync(serviceProvider);

        return Task.FromResult(new HelpAction().Invoke(parseResult));
    }
}
