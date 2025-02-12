using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.DotNet;

public static class DotNetSetup
{
    public static IServiceCollection AddDotNet(this IServiceCollection services)
    {
        return services.AddSingleton<IGlobalJson, GlobalJson>().AddSingleton<IDotNetVersion, DotNetVersion>();
    }
}
