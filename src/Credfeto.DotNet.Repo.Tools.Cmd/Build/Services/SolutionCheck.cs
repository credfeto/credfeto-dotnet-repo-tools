using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Cmd.DotNet;
using Credfeto.DotNet.Repo.Tools.Cmd.Exceptions;
using Credfeto.DotNet.Repo.Tools.Cmd.Packages;
using FunFair.BuildCheck.Interfaces;
using FunFair.BuildCheck.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Build.Services;

public sealed class SolutionCheck : ISolutionCheck
{
    private static readonly IProjectClassifier ProjectClassifier = new ProjectClassifier();
    private readonly ILogger<SolutionCheck> _logger;

    public SolutionCheck(ILogger<SolutionCheck> logger)
    {
        this._logger = logger;
    }

    public async ValueTask PreCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, ILogger logger, CancellationToken cancellationToken)
    {
        IFrameworkSettings frameworkSettings = new FrameworkSettings(dotNetSettings);

        bool allOk = true;

        foreach (string solution in solutions)
        {
            // TODO: make sure that it allows different versions of packages.
            int errors = await CheckRunner.CheckAsync(solutionFileName: solution,
                                                      preReleaseBuild: true,
                                                      warningsAsErrors: true,
                                                      frameworkSettings: frameworkSettings,
                                                      projectClassifier: ProjectClassifier,
                                                      buildServiceProvider: BuildServiceProvider,
                                                      logger: this._logger,
                                                      cancellationToken: cancellationToken);

            if (errors != 0)
            {
                allOk = false;
            }
        }

        if (!allOk)
        {
            throw new SolutionCheckFailedException();
        }
    }

    public async ValueTask<bool> PostCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, ILogger logger, CancellationToken cancellationToken)
    {
        IFrameworkSettings frameworkSettings = new FrameworkSettings(dotNetSettings);

        bool allOk = true;

        foreach (string solution in solutions)
        {
            // TODO: make sure that it allows different versions of packages.
            int errors = await CheckRunner.CheckAsync(solutionFileName: solution,
                                                      preReleaseBuild: true,
                                                      warningsAsErrors: true,
                                                      frameworkSettings: frameworkSettings,
                                                      projectClassifier: ProjectClassifier,
                                                      buildServiceProvider: BuildServiceProvider,
                                                      logger: this._logger,
                                                      cancellationToken: cancellationToken);

            if (errors != 0)
            {
                allOk = false;
            }
        }

        return allOk;
    }

    public async ValueTask ReleaseCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, CancellationToken cancellationToken)
    {
        IFrameworkSettings frameworkSettings = new FrameworkSettings(dotNetSettings);
        bool allOk = true;

        foreach (string solution in solutions)
        {
            // TODO: make sure that it allows different versions of packages.
            int errors = await CheckRunner.CheckAsync(solutionFileName: solution,
                                                      preReleaseBuild: false,
                                                      warningsAsErrors: true,
                                                      frameworkSettings: frameworkSettings,
                                                      projectClassifier: ProjectClassifier,
                                                      buildServiceProvider: BuildServiceProvider,
                                                      logger: this._logger,
                                                      cancellationToken: cancellationToken);

            if (errors != 0)
            {
                allOk = false;
            }
        }

        if (!allOk)
        {
            throw new SolutionCheckFailedException();
        }
    }

    private static ServiceProvider BuildServiceProvider(IServiceCollection serviceCollection)
    {
        return serviceCollection.BuildServiceProvider();
    }
}