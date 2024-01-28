using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public static class TemplateUpdateSetup
{
    public static IServiceCollection AddTemplateUpdate(this IServiceCollection services)
    {
        return services.AddSingleton<IBulkTemplateUpdater, BulkTemplateUpdater>()
                       .AddSingleton<IFileUpdater, FileUpdater>();
    }
}