using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Services;

internal interface IDotNetCommandRunner
{
    ValueTask<(string[] Output, int ExitCode)> RunAsync(string arguments, CancellationToken cancellationToken);
}
