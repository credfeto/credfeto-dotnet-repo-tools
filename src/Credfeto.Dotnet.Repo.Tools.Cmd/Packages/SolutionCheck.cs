using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;
using FunFair.BuildCheck.Interfaces;
using FunFair.BuildCheck.Runner;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

internal static class SolutionCheck
{
    private static readonly IFrameworkSettings FrameworkSettings = new FrameworkSettings();

    private static readonly IProjectClassifier ProjectClassifier = new ProjectClassifier();

    public static async ValueTask PreCheckAsync(IReadOnlyList<string> solutions, IDiagnosticLogger logging, CancellationToken cancellationToken)
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
                                                      logger: logging,
                                                      cancellationToken: cancellationToken);

            if (errors != 0)
            {
                allOk = false;

                break;
            }
        }

        if (!allOk)
        {
            throw new SolutionCheckFailedException();
        }
    }
}