using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Helpers;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.BuildCheck.Interfaces;
using FunFair.BuildCheck.Runner;
using FunFair.BuildCheck.Runner.Services;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Build.Services;

public sealed class DotNetSolutionCheck : IDotNetSolutionCheck
{
    private static readonly IProjectClassifier ProjectClassifier = new ProjectClassifier();

    private static readonly ICheckConfiguration PreReleaseCheckConfiguration = new CheckConfiguration(preReleaseBuild: true, allowPackageVersionMismatch: true);

    private static readonly ICheckConfiguration ReleaseCheckConfiguration = new CheckConfiguration(preReleaseBuild: false, allowPackageVersionMismatch: false);

    private readonly ILogger<DotNetSolutionCheck> _logger;

    private readonly IServiceProviderFactory _serviceProviderFactory;

    public DotNetSolutionCheck(IServiceProviderFactory serviceProviderFactory, ILogger<DotNetSolutionCheck> logger)
    {
        this._serviceProviderFactory = serviceProviderFactory;
        this._logger = logger;
    }

    public async ValueTask PreCheckAsync(IReadOnlyList<string> solutions,
                                         DotNetVersionSettings repositoryDotNetSettings,
                                         DotNetVersionSettings templateDotNetSettings,
                                         CancellationToken cancellationToken)
    {
        IFrameworkSettings frameworkSettings = FrameWorkSettingsBuilder.DefineFrameworkSettings(repositoryDotNetSettings: repositoryDotNetSettings, templateDotNetSettings: templateDotNetSettings);

        bool allOk = await this.CheckAllBuildAsync(solutions: solutions, frameworkSettings: frameworkSettings, checkConfiguration: ReleaseCheckConfiguration, cancellationToken: cancellationToken);

        if (!allOk)
        {
            throw new SolutionCheckFailedException(FailureReason(solutions: solutions, type: "Pre-Check"));
        }
    }

    public async ValueTask ReleaseCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings repositoryDotNetSettings, CancellationToken cancellationToken)
    {
        IFrameworkSettings frameworkSettings = new FrameworkSettings(repositoryDotNetSettings);

        bool allOk = await this.CheckAllBuildAsync(solutions: solutions, frameworkSettings: frameworkSettings, checkConfiguration: ReleaseCheckConfiguration, cancellationToken: cancellationToken);

        if (!allOk)
        {
            throw new SolutionCheckFailedException(FailureReason(solutions: solutions, type: "Release"));
        }
    }

    public ValueTask<bool> PostCheckAsync(IReadOnlyList<string> solutions,
                                          DotNetVersionSettings repositoryDotNetSettings,
                                          DotNetVersionSettings templateDotNetSettings,
                                          CancellationToken cancellationToken)
    {
        IFrameworkSettings frameworkSettings = FrameWorkSettingsBuilder.DefineFrameworkSettings(repositoryDotNetSettings: repositoryDotNetSettings, templateDotNetSettings: templateDotNetSettings);

        return this.CheckAllBuildAsync(solutions: solutions, frameworkSettings: frameworkSettings, checkConfiguration: PreReleaseCheckConfiguration, cancellationToken: cancellationToken);
    }

    private static string FailureReason(IReadOnlyList<string> solutions, string type)
    {
        return $"{type} {string.Join(separator: ", ", values: solutions)}";
    }

    private async ValueTask<bool> CheckAllBuildAsync(IReadOnlyList<string> solutions, IFrameworkSettings frameworkSettings, ICheckConfiguration checkConfiguration, CancellationToken cancellationToken)
    {
        bool allOk = true;

        foreach (string solution in solutions)
        {
            int errors = await CheckRunner.CheckAsync(solutionFileName: solution,
                                                      warningsAsErrors: true,
                                                      frameworkSettings: frameworkSettings,
                                                      projectClassifier: ProjectClassifier,
                                                      checkConfiguration: checkConfiguration,
                                                      buildServiceProvider: this._serviceProviderFactory.Build,
                                                      logger: this._logger,
                                                      cancellationToken: cancellationToken);

            if (errors != 0)
            {
                allOk = false;
            }
        }

        return allOk;
    }
}