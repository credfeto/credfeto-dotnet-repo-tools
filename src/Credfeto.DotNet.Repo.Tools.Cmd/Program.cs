using System;
using System.Threading;
using System.Threading.Tasks;
using Cocona;
using Cocona.Builder;
using Credfeto.DotNet.Repo.Tools.Cmd.Constants;
using Credfeto.DotNet.Repo.Tools.Packages.Exceptions;
using Credfeto.Package.Exceptions;

namespace Credfeto.DotNet.Repo.Tools.Cmd;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine($"{typeof(Program).Namespace} {ExecutableVersionInformation.ProgramVersion()}");
        Console.WriteLine();

        try
        {
            CoconaAppBuilder builder = CoconaApp.CreateBuilder(args);
            builder.Services.AddServices();

            CoconaApp app = builder.Build();
            app.AddCommands<Commands>();

            await app.RunAsync(CancellationToken.None);

            return ExitCodes.Success;
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

            if (exception.StackTrace is not null)
            {
                Console.WriteLine(exception.StackTrace);
            }

            return ExitCodes.Error;
        }
    }
}