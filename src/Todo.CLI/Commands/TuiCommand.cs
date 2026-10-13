using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Todo.Tui;

namespace Todo.CLI.Commands;

public class TuiCommand : Command
{
    private readonly IServiceProvider _serviceProvider;

    public TuiCommand(IServiceProvider serviceProvider) : base("tui")
    {
        _serviceProvider = serviceProvider;
        Description = "Opens the full-screen terminal UI to browse, add, edit, complete and remove to do items.";
        this.SetHandler(RunAsync);
    }

    internal Task RunAsync(InvocationContext context) => RunTuiAsync(_serviceProvider, context);

    internal static async Task RunTuiAsync(IServiceProvider serviceProvider, InvocationContext context)
    {
        context.ExitCode = await TuiHost.RunAsync(serviceProvider, serviceProvider.GetService<IConfiguration>());
    }
}
