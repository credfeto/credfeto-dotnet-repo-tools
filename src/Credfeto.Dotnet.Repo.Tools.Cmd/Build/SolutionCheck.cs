using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;
using Credfeto.Dotnet.Repo.Tools.Cmd.Packages;
using FunFair.BuildCheck.Interfaces;
using FunFair.BuildCheck.Runner;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Build;

internal static class SolutionCheck
{
    private static readonly IFrameworkSettings FrameworkSettings = new FrameworkSettings();

    private static readonly IProjectClassifier ProjectClassifier = new ProjectClassifier();

    public static async ValueTask PreCheckAsync(IReadOnlyList<string> solutions, ILogger logger, CancellationToken cancellationToken)
    {
        bool allOk = true;

        foreach (string solution in solutions)
        {
            // TODO: make sure that it allows different versions of packages.
            int errors = await CheckRunner.CheckAsync(solutionFileName: solution,
                                                      preReleaseBuild: true,
                                                      warningsAsErrors: true,
                                                      frameworkSettings: FrameworkSettings,
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

    public static async ValueTask<bool> PostCheckAsync(IReadOnlyList<string> solutions, ILogger logger, CancellationToken cancellationToken)
    {
        bool allOk = true;

        foreach (string solution in solutions)
        {
            // TODO: make sure that it allows different versions of packages.
            int errors = await CheckRunner.CheckAsync(solutionFileName: solution,
                                                      preReleaseBuild: true,
                                                      warningsAsErrors: true,
                                                      frameworkSettings: FrameworkSettings,
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

    public static async ValueTask ReleaseCheckAsync(IReadOnlyList<string> solutions, ILogger logger, CancellationToken cancellationToken)
    {
        bool allOk = true;

        foreach (string solution in solutions)
        {
            // TODO: make sure that it allows different versions of packages.
            int errors = await CheckRunner.CheckAsync(solutionFileName: solution,
                                                      preReleaseBuild: false,
                                                      warningsAsErrors: true,
                                                      frameworkSettings: FrameworkSettings,
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