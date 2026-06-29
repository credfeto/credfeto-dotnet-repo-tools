using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Services;

internal sealed class CallbackInvokingDependencyReducer : IDependencyReducer
{
    public async ValueTask<bool> CheckReferencesAsync(
        DotNetFiles dotNetFiles,
        ReferenceConfig config,
        CancellationToken cancellationToken
    )
    {
        await config.OnSuccessfulRemoval("Test.csproj", "Removed redundant dependency", cancellationToken);

        return true;
    }
}
