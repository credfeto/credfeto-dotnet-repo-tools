using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dotnet.Repo.Tools.Cmd.DotNet;
using Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;
using Credfeto.Dotnet.Repo.Tools.Cmd.Packages;
using FunFair.BuildCheck.Interfaces;
using FunFair.BuildCheck.Runner;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Build;

internal static class SolutionCheck
{
    private static readonly IProjectClassifier ProjectClassifier = new ProjectClassifier();

    public static async ValueTask PreCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, ILogger logger, CancellationToken cancellationToken)
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
                                                      logger: logger,
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

    public static async ValueTask<bool> PostCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, ILogger logger, CancellationToken cancellationToken)
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
                                                      logger: logger,
                                                      cancellationToken: cancellationToken);

            if (errors != 0)
            {
                allOk = false;
            }
        }

        return allOk;
    }

    public static async ValueTask ReleaseCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, ILogger logger, CancellationToken cancellationToken)
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
                                                      logger: logger,
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
}