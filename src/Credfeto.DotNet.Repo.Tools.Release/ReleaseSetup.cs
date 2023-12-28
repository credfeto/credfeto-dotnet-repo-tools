using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Release;

public static class ReleaseSetup
{
    public static IServiceCollection AddReleaseGeneration(this IServiceCollection services)
    {
        return services.AddSingleton<IReleaseConfigLoader, ReleaseConfigLoader>()
                       .AddSingleton<IReleaseGeneration, ReleaseGeneration>();
    }
}