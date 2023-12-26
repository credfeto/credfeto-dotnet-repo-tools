using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Build;

public interface ISolutionCheck
{
    ValueTask PreCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, ILogger logger, CancellationToken cancellationToken);

    ValueTask<bool> PostCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, ILogger logger, CancellationToken cancellationToken);

    ValueTask ReleaseCheckAsync(IReadOnlyList<string> solutions, DotNetVersionSettings dotNetSettings, CancellationToken cancellationToken);
}