using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Todo.CLI.Commands;
using Todo.CLI.UI;
using Todo.Core;
using Todo.CLI.Auth;
using Todo.CLI;
using System;

// Later sources win: appsettings.json (optional, next to the executable), then the per-user settings file the UI
// writes when you choose a theme, then TODO_-prefixed environment variables (for example TODO_Tui__AltKeys=on).
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile(Todo.Tui.UserSettingsStore.DefaultPath, optional: true)
    .AddEnvironmentVariables("TODO_")
    .Build();

var services = new ServiceCollection()
    .AddSingleton<IConfiguration>(configuration)
    .AddSingleton<TodoCliConfiguration>()
    .AddSingleton(TodoCliAuthenticationProviderFactory.GetAuthenticationProvider)
    .AddTodoRepositories()
    .AddSingleton<IUserInteraction>(sp => 
    {
        var outputFormat = OutputFormat.Interactive;
        if (args != null && args.Length > 1)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if ((args[i] == "--output" || args[i] == "-o") && 
                    Enum.TryParse<OutputFormat>(args[i + 1], true, out var format))
                {
                    outputFormat = format;
                    break;
                }
            }
        }
        return new InquirerUserInteraction(outputFormat);
    });

var serviceProvider = services.BuildServiceProvider();

var todoCommand = new TodoCommand(serviceProvider);

// The standard defaults, but with the full set of help aliases and without registering with dotnet-suggest.
var parser = new CommandLineBuilder(todoCommand)
    .UseVersionOption()
    .UseHelp("-h", "-H", "--help", "--HELP", "-?")
    .UseEnvironmentVariableDirective()
    .UseParseDirective()
    .UseSuggestDirective()
    .UseTypoCorrections()
    .UseParseErrorReporting()
    .UseExceptionHandler()
    .CancelOnProcessTermination()
    .Build();

return await parser.InvokeAsync(args);
