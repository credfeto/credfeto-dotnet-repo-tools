using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dotnet.Repo.Git;

public static class GitRepoList
{
    public static async ValueTask<IReadOnlyList<string>> LoadRepoListAsync(string path, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> lines = await File.ReadAllLinesAsync(path, cancellationToken);

        return lines.Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToArray();
    }
}