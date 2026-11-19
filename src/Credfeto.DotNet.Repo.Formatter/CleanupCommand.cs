using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using Credfeto.DotNet.Repo.Formatter.Setup;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Formatter;

// The command-line surface of cscleanup. It only depends on the service provider, so --self-test can report a
// broken dependency graph itself instead of ConsoleAppFramework failing to construct the command class.
public sealed class CleanupCommand
{
    private readonly IServiceProvider _serviceProvider;

    public CleanupCommand(IServiceProvider serviceProvider)
    {
        this._serviceProvider = serviceProvider;
    }

    [Command("")]
    [SuppressMessage(
        category: "FunFair.CodeAnalysis",
        checkId: "FFS0020: CancellationToken should be the last parameter",
        Justification = "ConsoleAppFramework binds the positional inputs from a params array, which C# requires to be last"
    )]
    public Task<int> CleanupAsync(
        bool removeSuppressions = false,
        string? buildRoot = null,
        bool selfTest = false,
        CancellationToken cancellationToken = default,
        [Argument] params string[] inputs
    )
    {
        if (selfTest)
        {
            return Task.FromResult(
                StartupSelfTest.Run(serviceProvider: this._serviceProvider, errorOutput: Console.Error)
            );
        }

        Commands commands = ActivatorUtilities.CreateInstance<Commands>(this._serviceProvider);

        return commands.CleanupAsync(
            inputs: inputs,
            removeSuppressions: removeSuppressions,
            buildRoot: buildRoot,
            cancellationToken: cancellationToken
        );
    }
}
