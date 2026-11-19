using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using Credfeto.DotNet.Repo.Formatter.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Formatter.Setup;

public static class CommandLineRunner
{
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

        if (CommandLineCompatibility.IsHelpRequest(args))
        {
            string application = typeof(CommandLineRunner).Assembly.GetName().Name ?? string.Empty;
            string help = CommandLineHelp.RenderCommand(
                application: application,
                command: CommandLineSpecification.Cleanup
            );
            await output.WriteLineAsync(help.AsMemory(), cancellationToken: cancellationToken);

            return ExitCodes.HelpShown;
        }

        ConsoleApp.ConsoleAppBuilder app = ConsoleApp
            .Create()
            .ConfigureLogging(ConfigureLogging)
            .ConfigureServices(configureServices)
            .PostConfigureServices(InitializeLogging);

        app.Add<CleanupCommand>();

        await app.RunAsync(args: [.. args], cancellationToken: cancellationToken);

        return Environment.ExitCode;
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
