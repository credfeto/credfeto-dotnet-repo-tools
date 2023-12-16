using System;
using Credfeto.Date;
using Credfeto.Dotnet.Repo.Tools.Cmd.Services;
using Credfeto.Package;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dotnet.Repo.Tools.Cmd;

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
                                      .BuildServiceProvider();
    }
}