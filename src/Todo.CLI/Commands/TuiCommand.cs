using System;
using System.CommandLine;
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
        SetAction(_ => RunAsync());
    }

    internal Task<int> RunAsync() => RunTuiAsync(_serviceProvider);

    internal static Task<int> RunTuiAsync(IServiceProvider serviceProvider) =>
        TuiHost.RunAsync(serviceProvider, serviceProvider.GetService<IConfiguration>());
}
