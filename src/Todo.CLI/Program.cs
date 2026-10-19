using System;
using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Todo.CLI;
using Todo.CLI.Auth;
using Todo.CLI.Commands;
using Todo.CLI.UI;
using Todo.Core;

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

return await todoCommand.Parse(args).InvokeAsync();
