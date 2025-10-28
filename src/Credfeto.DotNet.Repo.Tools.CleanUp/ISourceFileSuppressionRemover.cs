using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.CleanUp;

public interface ISourceFileSuppressionRemover
{
    ValueTask<string> RemoveSuppressionsAsync(string fileName, string content, BuildContext buildContext, CancellationToken cancellationToken);
}