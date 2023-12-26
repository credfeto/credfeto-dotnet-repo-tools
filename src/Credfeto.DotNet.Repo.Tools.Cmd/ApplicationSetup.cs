using System;
using Credfeto.Date;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Cmd.Packages;
using Credfeto.DotNet.Repo.Tools.Cmd.Packages.Services;
using Credfeto.DotNet.Repo.Tools.Cmd.Services;
using Credfeto.DotNet.Repo.Tracking;
using Credfeto.Package;
using FunFair.BuildVersion.Detection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Cmd;

internal static class ApplicationSetup
{
    public static IServiceProvider Setup(bool warningsAsErrors)
    {
        DiagnosticLogger logger = new(warningsAsErrors);

        return new ServiceCollection().AddSingleton<ILogger>(logger)
                                      .AddSingleton<IDiagnosticLogger>(logger)
                                      .AddSingleton(typeof(ILogger<>), typeof(LoggerProxy<>))
                                      .AddDate()
                                      .AddPackageUpdater()
                                      .AddTracking()
                                      .AddDotNet()
                                      .AddBuild()
                                      .AddBuildVersionDetection(new BranchSettings(releaseSuffix: null, package: null))
                                      .AddReleaseGeneration()
                                      .AddSingleton<IBulkPackageUpdater, BulkPackageUpdater>()
                                      .AddSingleton<IServiceProviderFactory, ServiceProviderFactory>()
                                      .BuildServiceProvider();
    }
}