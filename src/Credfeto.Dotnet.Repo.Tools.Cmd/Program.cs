using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Credfeto.Dotnet.Repo.Git;
using Credfeto.Dotnet.Repo.Tools.Cmd.DotNet;
using Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;
using Credfeto.Dotnet.Repo.Tools.Cmd.Packages;
using Credfeto.Dotnet.Repo.Tracking;
using Credfeto.Package;
using Credfeto.Package.Exceptions;
using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Dotnet.Repo.Tools.Cmd;

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

        if (options.Require(accessor: o => o.Work, out string? workFolder) && options.Require(accessor: o => o.Repositories, out string? repositoriesFileName) &&
            options.Require(accessor: o => o.Packages, out string? packagesFileName) && options.Require(accessor: o => o.Tracking, out string? trackingFileName) &&
            options.Require(accessor: o => o.Template, out string? templateRepository))
        {
            await PerformUpdatesAsync(options: options,
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

    private static async ValueTask PerformUpdatesAsync(Options options,
                                                       string repositoriesFileName,
                                                       string templateRepository,
                                                       string? cacheFileName,
                                                       string trackingFileName,
                                                       string packagesFileName,
                                                       string workFolder,
                                                       CancellationToken cancellationToken)
    {
        IServiceProvider services = ApplicationSetup.Setup(false);

        IReadOnlyList<string> repos = ExcludeTemplateRepo(await GitRepoList.LoadRepoListAsync(path: repositoriesFileName, cancellationToken: cancellationToken),
                                                          templateRepository: templateRepository);

        if (repos.Count == 0)
        {
            throw new InvalidOperationException("No Repositories found");
        }

        IPackageCache packageCache = services.GetRequiredService<IPackageCache>();
        ITrackingCache trackingCache = services.GetRequiredService<ITrackingCache>();

        await LoadPackageCacheAsync(packageCacheFile: cacheFileName, packageCache: packageCache, cancellationToken: cancellationToken);
        await LoadTrackingCacheAsync(trackingFile: trackingFileName, trackingCache: trackingCache, cancellationToken: cancellationToken);

        IReadOnlyList<PackageUpdate> packages = await LoadPackageUpdateConfigAsync(filename: packagesFileName, cancellationToken: cancellationToken);

        IUpdater updater = services.GetRequiredService<IUpdater>();

        using (Repository templateRepo = await GitUtils.OpenOrCloneAsync(workDir: workFolder, repoUrl: templateRepository, cancellationToken: cancellationToken))
        {
            UpdateContext updateContext = await BuildUpdateContextAsync(options: options,
                                                                        templateRepo: templateRepo,
                                                                        workFolder: workFolder,
                                                                        trackingFileName: trackingFileName,
                                                                        cancellationToken: cancellationToken);

            try
            {
                await updater.UpdateRepositoriesAsync(updateContext: updateContext, repositories: repos, packages: packages, cancellationToken: cancellationToken);
            }
            finally
            {
                await SavePackageCacheAsync(packageCacheFile: cacheFileName, packageCache: packageCache, cancellationToken: cancellationToken);
                await SaveTrackingCacheAsync(trackingFile: trackingFileName, trackingCache: trackingCache, cancellationToken: cancellationToken);
            }
        }
    }

    private static IReadOnlyList<string> ExcludeTemplateRepo(IReadOnlyList<string> repositories, string templateRepository)
    {
        return repositories.Where(repositoryUrl => !StringComparer.InvariantCultureIgnoreCase.Equals(x: templateRepository, y: repositoryUrl))
                           .ToArray();
    }

    private static async ValueTask<UpdateContext> BuildUpdateContextAsync(Options options, Repository templateRepo, string workFolder, string trackingFileName, CancellationToken cancellationToken)
    {
        DotNetVersionSettings dotNetSettings = await GlobalJson.LoadGlobalJsonAsync(baseFolder: templateRepo.Info.WorkingDirectory, cancellationToken: cancellationToken);

        // TODO: check to see what SDKs are installed throw if the one in the sdk isn't installed.

        return new(WorkFolder: workFolder,
                   CacheFileName: options.Cache,
                   TrackingFileName: trackingFileName,
                   DotNetSettings: dotNetSettings,
                   AdditionalSources: options.Source?.ToArray() ?? Array.Empty<string>());
    }

    private static ValueTask SaveTrackingCacheAsync(string? trackingFile, ITrackingCache trackingCache, in CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(trackingFile)
            ? ValueTask.CompletedTask
            : trackingCache.SaveAsync(fileName: trackingFile, cancellationToken: cancellationToken);
    }

    private static async ValueTask SavePackageCacheAsync(string? packageCacheFile, IPackageCache packageCache, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageCacheFile))
        {
            return;
        }

        await packageCache.SaveAsync(fileName: packageCacheFile, cancellationToken: cancellationToken);
    }

    private static ValueTask LoadTrackingCacheAsync(string? trackingFile, ITrackingCache trackingCache, in CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackingFile))
        {
            return ValueTask.CompletedTask;
        }

        if (!File.Exists(trackingFile))
        {
            return ValueTask.CompletedTask;
        }

        return trackingCache.LoadAsync(fileName: trackingFile, cancellationToken: cancellationToken);
    }

    private static async ValueTask LoadPackageCacheAsync(string? packageCacheFile, IPackageCache packageCache, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageCacheFile))
        {
            return;
        }

        if (!File.Exists(packageCacheFile))
        {
            return;
        }

        await packageCache.LoadAsync(fileName: packageCacheFile, cancellationToken: cancellationToken);
    }

    private static async ValueTask<IReadOnlyList<PackageUpdate>> LoadPackageUpdateConfigAsync(string filename, CancellationToken cancellationToken)
    {
        byte[] content = await File.ReadAllBytesAsync(path: filename, cancellationToken: cancellationToken);

        IReadOnlyList<PackageUpdate> packages = JsonSerializer.Deserialize(utf8Json: content, jsonTypeInfo: PackageConfigSerializationContext.Default.IReadOnlyListPackageUpdate) ??
                                                Array.Empty<PackageUpdate>();

        if (packages.Count == 0)
        {
            throw new InvalidOperationException("No packages found");
        }

        return packages;
    }
}