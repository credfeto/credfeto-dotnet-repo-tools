using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Build.Services;

public sealed class ProjectFinder : IProjectFinder
{
    public ValueTask<IReadOnlyList<string>> FindProjectsAsync(string basePath, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> projects = Directory.GetFiles(
            path: basePath,
            searchPattern: "*.csproj",
            searchOption: SearchOption.AllDirectories
        );

        return ValueTask.FromResult(projects);
    }
}
