using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces;

public interface IDotNetSolutionCheck
{
    ValueTask PreCheckAsync(IReadOnlyList<string> solutions,
                            DotNetVersionSettings repositoryDotNetSettings,
                            DotNetVersionSettings templateDotNetSettings,
                            CancellationToken cancellationToken);

    ValueTask<bool> PostCheckAsync(IReadOnlyList<string> solutions,
                                   DotNetVersionSettings repositoryDotNetSettings,
                                   DotNetVersionSettings templateDotNetSettings,
                                   CancellationToken cancellationToken);

    ValueTask ReleaseCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings repositoryDotNetSettings, CancellationToken cancellationToken);
}