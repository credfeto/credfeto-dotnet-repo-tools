using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Packages;

public static class PackagesSetup
{
    public static IServiceCollection AddBulkPackageUpdater(this IServiceCollection services)
    {
        return services.AddSingleton<IBulkPackageUpdater, BulkPackageUpdater>();
    }
}