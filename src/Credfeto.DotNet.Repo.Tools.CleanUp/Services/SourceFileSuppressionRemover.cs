using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed class SourceFileSuppressionRemover : ISourceFileSuppressionRemover
{
    public async ValueTask<string> RemoveSuppressionsAsync(string fileName, string content, CancellationToken cancellationToken)
    {
        await Task.Delay(millisecondsDelay: 1, cancellationToken: cancellationToken);

        return content;
    }
}