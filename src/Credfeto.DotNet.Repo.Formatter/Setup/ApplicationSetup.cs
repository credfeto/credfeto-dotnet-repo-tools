using Credfeto.DotNet.Repo.Tools.Build;
using Credfeto.DotNet.Repo.Tools.CleanUp;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Formatter.Setup;

internal static class ApplicationSetup
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddBuild().AddCleanUp();
    }
}
