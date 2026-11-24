using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using Credfeto.DotNet.Repo.Tools.Cmd.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Setup;

public static class CommandLineRunner
{
    private const string VERSION_OPTION = "--version";

    // ConsoleAppFramework keeps its log writers, service provider and exit code in process-wide state,
    // so callers (the tests) must not run two command lines at once.
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        Action<IServiceCollection> configureServices,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        ConsoleApp.Log = output.WriteLine;
        ConsoleApp.LogError = error.WriteLine;
        ConsoleApp.Version = CommandLineCompatibility.ProductVersion(typeof(CommandLineRunner).Assembly);
        Environment.ExitCode = ExitCodes.Success;

        CommandLineCommand? command = args is [{ } name, ..] ? CommandLineSpecification.Find(name) : null;

        if (CommandLineCompatibility.IsHelpRequest(args) || args is [])
        {
            await WriteAsync(writer: output, text: Help(command), cancellationToken: cancellationToken);

            return args is [] ? ExitCodes.Success : ExitCodes.HelpShown;
        }

        if (FindUsageError(args: args, command: command) is { } usageError)
        {
            await WriteAsync(writer: error, text: usageError, cancellationToken: cancellationToken);

            return ExitCodes.Error;
        }

        ConsoleApp.ConsoleAppBuilder app = ConsoleApp
            .Create()
            .ConfigureLogging(ConfigureLogging)
            .ConfigureServices(configureServices)
            .PostConfigureServices(InitializeLogging);

        app.Add<Commands>();

        await app.RunAsync(
            args: NormaliseArguments(args: args, command: command),
            cancellationToken: cancellationToken
        );

        return Environment.ExitCode;
    }

    private static string Help(CommandLineCommand? command)
    {
        string application = typeof(CommandLineRunner).Assembly.GetName().Name ?? string.Empty;

        return command is null
            ? CommandLineHelp.RenderCommandList(application: application, commands: CommandLineSpecification.Commands)
            : CommandLineHelp.RenderCommand(application: application, command: command);
    }

    // ConsoleAppFramework shows the help and exits 0 for an unknown command or a command given no options;
    // Cocona reported those as errors, which scripts rely on.
    private static string? FindUsageError(IReadOnlyList<string> args, CommandLineCommand? command)
    {
        if (command is null)
        {
            return args[0] switch
            {
                VERSION_OPTION when args.Count == 1 => null,
                ['-', ..] => $"Error: Unknown option '{args[0]}'",
                _ => $"Error: '{args[0]}' is not a command. See '--help' for usage.",
            };
        }

        return args.Count == 1 ? $"Error: '{args[0]}' requires options. See '{args[0]} --help' for usage." : null;
    }

    private static string[] NormaliseArguments(IReadOnlyList<string> args, CommandLineCommand? command)
    {
        string[] expanded = command?.ExpandShortNames(args) ?? [.. args];

        // Cocona collected a repeated --source into a list; ConsoleAppFramework keeps the last value of a repeated
        // option, but splits a comma-separated value into an array, so the repeats are joined into one value.
        return CommandLineCompatibility.JoinRepeatedOption(args: expanded, longName: "--source");
    }

    private static ValueTask WriteAsync(TextWriter writer, string text, CancellationToken cancellationToken)
    {
        return new(writer.WriteLineAsync(text.AsMemory(), cancellationToken: cancellationToken));
    }

    private static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging
            .AddFilter(category: "Microsoft", level: LogLevel.Warning)
            .AddFilter(category: "System.Net.Http.HttpClient", level: LogLevel.Warning)
            .ClearProviders();
    }

    private static void InitializeLogging(IServiceProvider serviceProvider)
    {
        Logging.InitializeLogging(serviceProvider.GetRequiredService<ILoggerFactory>());
    }
}
