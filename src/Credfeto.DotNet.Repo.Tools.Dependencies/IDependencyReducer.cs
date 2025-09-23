using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;

namespace Credfeto.DotNet.Repo.Tools.Dependencies;

public interface IDependencyReducer
{
    ValueTask<bool> CheckReferencesAsync(DotNetFiles dotNetFiles, ReferenceConfig config, CancellationToken cancellationToken);
}