using Credfeto.Date;
using Credfeto.DotNet.Repo.Tools.Build;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp;
using Credfeto.DotNet.Repo.Tools.Cmd.Services;
using Credfeto.DotNet.Repo.Tools.Dependencies;
using Credfeto.DotNet.Repo.Tools.DotNet;
using Credfeto.DotNet.Repo.Tools.Git;
using Credfeto.DotNet.Repo.Tools.Packages;
using Credfeto.DotNet.Repo.Tools.Release;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate;
using Credfeto.DotNet.Repo.Tracking;
using Credfeto.Package;
using FunFair.BuildVersion.Detection;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Setup;

internal static class ApplicationSetup
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services
            .AddDate()
            .AddGit()
            .AddPackageUpdater()
            .AddTracking()
            .AddDotNet()
            .AddBuild()
            .AddBuildVersionDetection(new BranchSettings(releaseSuffix: null, package: null))
            .AddReleaseGeneration()
            .AddBulkPackageUpdater()
            .AddCleanUp()
            .AddTemplateUpdate()
            .AddDependenciesReduction()
            .AddSingleton<IServiceProviderFactory, ServiceProviderFactory>();
    }
}
