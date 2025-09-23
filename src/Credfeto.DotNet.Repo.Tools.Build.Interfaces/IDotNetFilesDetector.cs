using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces;

public interface IDotNetFilesDetector
{
    ValueTask<DotNetFiles> FindAsync(string baseFolder, CancellationToken cancellationToken);
}