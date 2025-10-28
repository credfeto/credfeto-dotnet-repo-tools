using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.CleanUp;

public interface ISourceFileSuppressionRemover
{
    ValueTask<string> RemoveSuppressionsAsync(string fileName, string content, CancellationToken cancellationToken);
}