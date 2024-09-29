using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.BuildCheck.Interfaces;
using FunFair.BuildCheck.Runner;
using FunFair.BuildCheck.Runner.Services;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

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

    public async ValueTask PreCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, BuildSettings buildSettings, CancellationToken cancellationToken)
    {
        IFrameworkSettings frameworkSettings = DefineFrameworkSettings(dotNetSettings: dotNetSettings, buildSettings: buildSettings);

        bool allOk = true;

        foreach (string solution in solutions)
        {
            int errors = await CheckRunner.CheckAsync(solutionFileName: solution,
                                                      warningsAsErrors: true,
                                                      frameworkSettings: frameworkSettings,
                                                      projectClassifier: ProjectClassifier,
                                                      checkConfiguration: PreReleaseCheckConfiguration,
                                                      buildServiceProvider: this._serviceProviderFactory.Build,
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

    public async ValueTask ReleaseCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, CancellationToken cancellationToken)
    {
        IFrameworkSettings frameworkSettings = new FrameworkSettings(dotNetSettings);
        bool allOk = true;

        foreach (string solution in solutions)
        {
            int errors = await CheckRunner.CheckAsync(solutionFileName: solution,
                                                      warningsAsErrors: true,
                                                      frameworkSettings: frameworkSettings,
                                                      projectClassifier: ProjectClassifier,
                                                      checkConfiguration: ReleaseCheckConfiguration,
                                                      buildServiceProvider: this._serviceProviderFactory.Build,
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

    public async ValueTask<bool> PostCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, BuildSettings buildSettings, CancellationToken cancellationToken)
    {
        IFrameworkSettings frameworkSettings = DefineFrameworkSettings(dotNetSettings: dotNetSettings, buildSettings: buildSettings);

        bool allOk = true;

        foreach (string solution in solutions)
        {
            int errors = await CheckRunner.CheckAsync(solutionFileName: solution,
                                                      warningsAsErrors: true,
                                                      frameworkSettings: frameworkSettings,
                                                      projectClassifier: ProjectClassifier,
                                                      checkConfiguration: PreReleaseCheckConfiguration,
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

    private static IFrameworkSettings DefineFrameworkSettings(in DotNetVersionSettings dotNetSettings, in BuildSettings buildSettings)
    {
        if (!string.IsNullOrEmpty(dotNetSettings.SdkVersion) && !string.IsNullOrEmpty(buildSettings.Framework))
        {
            NuGetVersion standardFramework = new(dotNetSettings.SdkVersion);
            NuGetVersion buildFramework = new(buildSettings.Framework);

            if (buildFramework.IsPrerelease && buildFramework > standardFramework)
            {
                return new FrameworkSettings(buildSettings: buildSettings, allowPreRelease: buildFramework.IsPrerelease);
            }
        }

        return new FrameworkSettings(dotNetSettings);
    }
}