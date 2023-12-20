using Credfeto.Dotnet.Repo.Tracking.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Dotnet.Repo.Tracking;

public static class TrackingSetup
{
    public static IServiceCollection AddTracking(this IServiceCollection services)
    {
        return services.AddSingleton<ITrackingCache, TrackingCache>();
    }
}