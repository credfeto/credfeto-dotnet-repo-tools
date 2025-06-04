using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Services;
using FunFair.BuildCheck.Interfaces;
using FunFair.BuildCheck.Runner.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Build;

public static class BuildSetup
{
    public static IServiceCollection AddBuild(this IServiceCollection services)
    {
        return services
            .AddSingleton<IDotNetSolutionCheck, DotNetSolutionCheck>()
            .AddSingleton<IDotNetBuild, DotNetBuild>()
            .AddSingleton<IProjectXmlLoader, ProjectXmlLoader>();
    }
}
