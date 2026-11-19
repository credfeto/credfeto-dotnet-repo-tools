using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Formatter.Constants;
using Credfeto.DotNet.Repo.Formatter.Setup;

namespace Credfeto.DotNet.Repo.Formatter;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine($"{VersionInformation.Product} {VersionInformation.Version}");
        Console.WriteLine();

        try
        {
            return await CommandLineRunner.RunAsync(
                args: args,
                configureServices: static services => services.AddServices(),
                output: Console.Out,
                error: Console.Error,
                cancellationToken: CancellationToken.None
            );
        }
        catch (Exception exception)
        {
            Console.WriteLine($"ERROR: {exception.Message}");

            return ExitCodes.Error;
        }
    }
}
