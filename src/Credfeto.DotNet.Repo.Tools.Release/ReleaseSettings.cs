using Credfeto.DotNet.Repo.Git.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Git;

public static class ReleaseSettings
{
    public static IServiceCollection AddReleaseGeneration(this IServiceCollection services)
    {
        services.AddSingleton<IReleaseGeneration, ReleaseGeneration>();

        return services;
    }
}