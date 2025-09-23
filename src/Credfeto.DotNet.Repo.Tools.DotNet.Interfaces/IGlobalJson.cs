using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;

public interface IGlobalJson
{
    ValueTask<DotNetVersionSettings> LoadGlobalJsonAsync(string baseFolder, CancellationToken cancellationToken);
}