using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Git;

public static class GitSetup
{
    public static IServiceCollection AddGit(this IServiceCollection services)
    {
        return services.AddSingleton<IGitRepositoryFactory, GitRepositoryFactory>()
                       .AddSingleton<IGitRepositoryLocator, GitRepositoryLocator>();
    }
}