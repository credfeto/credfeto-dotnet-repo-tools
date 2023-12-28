using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Credfeto.DotNet.Repo.Tools.Cmd.Exceptions;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Exceptions;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.Package.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Cmd;

internal static class Program
{
    private const int SUCCESS = 0;
    private const int ERROR = -1;

    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine($"{typeof(Program).Namespace} {ExecutableVersionInformation.ProgramVersion()}");
        Console.WriteLine();

        try
        {
            ParserResult<Options> parser = await ParseOptionsAsync(args);

            return parser.Tag == ParserResultType.Parsed
                ? SUCCESS
                : ERROR;
        }
        catch (NoPackagesUpdatedException)
        {
            return ERROR;
        }
        catch (PackageUpdateException exception)
        {
            Console.WriteLine(exception.Message);

            return ERROR;
        }
        catch (UpdateFailedException exception)
        {
            Console.WriteLine(exception.Message);

            return ERROR;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"ERROR: {exception.Message}");

            if (exception.StackTrace is not null)
            {
                Console.WriteLine(exception.StackTrace);
            }

            return ERROR;
        }
    }

    private static Task<ParserResult<Options>> ParseOptionsAsync(string[] args)
    {
        return Parser.Default.ParseArguments<Options>(args)
                     .WithNotParsed(NotParsed)
                     .WithParsedAsync(ParsedOkAsync);
    }

    private static void NotParsed(IEnumerable<Error> errors)
    {
        Console.WriteLine("Errors:");

        foreach (Error error in errors)
        {
            Console.WriteLine($" * {error.Tag.GetName()}");
        }
    }

    private static bool Require(this Options options, Func<Options, string?> accessor, [NotNullWhen(true)] out string? value)
    {
        string? v = accessor(options);

        if (string.IsNullOrWhiteSpace(v))
        {
            value = null;

            return false;
        }

        value = v;

        return true;
    }

    private static async Task ParsedOkAsync(Options options)
    {
        CancellationToken cancellationToken = CancellationToken.None;

        if (IsBulkPackageUpdate(options: options, out string? workFolder, out string? repositoriesFileName, out string? packagesFileName, out string? trackingFileName, out string? templateRepository))
        {
            await PerformBulkPackageUpdatesAsync(options: options,
                                                 repositoriesFileName: repositoriesFileName,
                                                 templateRepository: templateRepository,
                                                 cacheFileName: options.Cache,
                                                 trackingFileName: trackingFileName,
                                                 packagesFileName: packagesFileName,
                                                 workFolder: workFolder,
                                                 cancellationToken: cancellationToken);

            return;
        }

        throw new InvalidOptionsException("No valid option selected");
    }

    private static bool IsBulkPackageUpdate(Options options,
                                            [NotNullWhen(true)] out string? workFolder,
                                            [NotNullWhen(true)] out string? repositoriesFileName,
                                            [NotNullWhen(true)] out string? packagesFileName,
                                            [NotNullWhen(true)] out string? trackingFileName,
                                            [NotNullWhen(true)] out string? templateRepository)
    {
        bool hasWork = options.Require(accessor: o => o.Work, value: out workFolder);
        bool hasRepositories = options.Require(accessor: o => o.Repositories, value: out repositoriesFileName);
        bool hasPackages = options.Require(accessor: o => o.Packages, value: out packagesFileName);
        bool hasTracking = options.Require(accessor: o => o.Tracking, value: out trackingFileName);
        bool hasTemplate = options.Require(accessor: o => o.Template, value: out templateRepository);

        return hasWork && hasRepositories && hasPackages && hasTracking && hasTemplate;
    }

    private static async ValueTask PerformBulkPackageUpdatesAsync(Options options,
                                                                  string repositoriesFileName,
                                                                  string templateRepository,
                                                                  string? cacheFileName,
                                                                  string trackingFileName,
                                                                  string packagesFileName,
                                                                  string workFolder,
                                                                  CancellationToken cancellationToken)
    {
        IServiceProvider services = ApplicationSetup.Setup(false);

        IGitRepositoryListLoader gitRepositoryListLoader = services.GetRequiredService<IGitRepositoryListLoader>();

        IReadOnlyList<string> repositories = ExcludeTemplateRepo(await gitRepositoryListLoader.LoadAsync(path: repositoriesFileName, cancellationToken: cancellationToken),
                                                                 templateRepository: templateRepository);

        if (repositories.Count == 0)
        {
            throw new InvalidOperationException("No Repositories found");
        }

        IBulkPackageUpdater bulkPackageUpdater = services.GetRequiredService<IBulkPackageUpdater>();

        await bulkPackageUpdater.BulkUpdateAsync(additionalNugetSources: options.Source?.ToArray() ?? Array.Empty<string>(),
                                                 templateRepository: templateRepository,
                                                 cacheFileName: cacheFileName,
                                                 trackingFileName: trackingFileName,
                                                 packagesFileName: packagesFileName,
                                                 workFolder: workFolder,
                                                 repositories: repositories,
                                                 cancellationToken: cancellationToken);
    }

    private static IReadOnlyList<string> ExcludeTemplateRepo(IReadOnlyList<string> repositories, string templateRepository)
    {
        return repositories.Where(repositoryUrl => !StringComparer.InvariantCultureIgnoreCase.Equals(x: templateRepository, y: repositoryUrl))
                           .ToArray();
    }
}