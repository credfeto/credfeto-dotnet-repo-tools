using Credfeto.DotNet.Repo.Tools.Build.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Build;

public static class BuildSetup
{
    public static IServiceCollection AddBuild(this IServiceCollection services)
    {
        return services.AddSingleton<ISolutionCheck, SolutionCheck>()
                       .AddSingleton<IDotNetBuild, DotNetBuild>();
    }
}