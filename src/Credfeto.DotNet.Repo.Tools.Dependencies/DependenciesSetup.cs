using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Dependencies;

public static class DependenciesSetup
{
    public static IServiceCollection AddDependenciesReduction(this IServiceCollection services)
    {
        return services.AddSingleton<IBulkDependencyReducer, BulkDependencyReducer>();
    }
}