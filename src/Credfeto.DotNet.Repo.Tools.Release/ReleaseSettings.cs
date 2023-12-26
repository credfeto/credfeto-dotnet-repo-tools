using Credfeto.DotNet.Repo.Tools.Release.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Release;

public static class ReleaseSettings
{
    public static IServiceCollection AddReleaseGeneration(this IServiceCollection services)
    {
        services.AddSingleton<IReleaseGeneration, ReleaseGeneration>();

        return services;
    }
}