using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;

public interface IDotNetFilesDetector
{
    ValueTask<DotNetFiles?> FindAsync(string baseFolder, in CancellationToken cancellationToken);
}