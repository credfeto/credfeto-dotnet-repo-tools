using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.CleanUp;

public interface ISourceFileReformatter
{
    ValueTask<string> ReformatAsync(string content, string fileName, CancellationToken cancellationToken);
}