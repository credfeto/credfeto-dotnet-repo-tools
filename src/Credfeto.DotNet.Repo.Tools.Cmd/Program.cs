using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Cmd.Constants;
using Credfeto.DotNet.Repo.Tools.Cmd.Setup;
using Credfeto.DotNet.Repo.Tools.Packages.Exceptions;
using Credfeto.Package.Exceptions;

namespace Credfeto.DotNet.Repo.Tools.Cmd;

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
        catch (NoPackagesUpdatedException)
        {
            return ExitCodes.Error;
        }
        catch (PackageUpdateException exception)
        {
            Console.WriteLine(exception.Message);

            return ExitCodes.Error;
        }
        catch (UpdateFailedException exception)
        {
            Console.WriteLine(exception.Message);

            return ExitCodes.Error;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"ERROR: {exception.Message}");

            return ExitCodes.Error;
        }
    }
}
