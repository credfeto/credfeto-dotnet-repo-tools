using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.Cmd.Constants;
using Credfeto.DotNet.Repo.Tools.Dependencies;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Setup;

// Builds the same container the commands run in and constructs the commands from it, so a published (trimmed or
// native AOT) binary can prove its services still resolve without running a real command.
internal static class SelfTest
{
    public static async Task<int> RunAsync(
        Action<ILoggingBuilder> configureLogging,
        Action<IServiceCollection> configureServices,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await using (ServiceProvider serviceProvider = BuildServiceProvider(configureLogging, configureServices))
            {
                Logging.InitializeLogging(serviceProvider.GetRequiredService<ILoggerFactory>());

                _ = CreateCommands(serviceProvider);
            }

            await output.WriteLineAsync("Self test passed".AsMemory(), cancellationToken: cancellationToken);

            return ExitCodes.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await error.WriteLineAsync(
                $"Self test failed: {exception.Message}".AsMemory(),
                cancellationToken: cancellationToken
            );

            return ExitCodes.Error;
        }
    }

    private static ServiceProvider BuildServiceProvider(
        Action<ILoggingBuilder> configureLogging,
        Action<IServiceCollection> configureServices
    )
    {
        ServiceCollection services = new();
        services.AddLogging(configureLogging);
        configureServices(services);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private static Commands CreateCommands(IServiceProvider serviceProvider)
    {
        return new(
            gitRepositoryListLoader: serviceProvider.GetRequiredService<IGitRepositoryListLoader>(),
            bulkCodeCleanUp: serviceProvider.GetRequiredService<IBulkCodeCleanUp>(),
            bulkPackageUpdater: serviceProvider.GetRequiredService<IBulkPackageUpdater>(),
            bulkTemplateUpdater: serviceProvider.GetRequiredService<IBulkTemplateUpdater>(),
            bulkDependencyReducer: serviceProvider.GetRequiredService<IBulkDependencyReducer>(),
            dependencyReducer: serviceProvider.GetRequiredService<IDependencyReducer>(),
            dotNetFilesDetector: serviceProvider.GetRequiredService<IDotNetFilesDetector>(),
            gitRepositoryFactory: serviceProvider.GetRequiredService<IGitRepositoryFactory>(),
            logger: serviceProvider.GetRequiredService<ILogger<Commands>>()
        );
    }
}
