using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Setup;

internal static class Logging
{
    [SuppressMessage(category: "Microsoft.Reliability", checkId: "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Lives for program lifetime")]
    [SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global", Justification = "Not easily testable as uses third party services")]
    [SuppressMessage(category: "SmartAnalyzers.CSharpExtensions.Annotations", checkId: "CSE007:DisposeObjectsBeforeLosingScope", Justification = "Lives for program lifetime")]
    public static void InitializeLogging(ILoggerFactory loggerFactory)
    {
        // set up the logger factory
        loggerFactory.AddSerilog(CreateLogger(), dispose: true);
    }

    private static Logger CreateLogger()
    {
        return new LoggerConfiguration().Enrich.FromLogContext()
                                        .Enrich.WithDemystifiedStackTraces()
                                        .Enrich.WithMachineName()
                                        .Enrich.WithProcessId()
                                        .Enrich.WithThreadId()
                                        .Enrich.WithProperty(name: "ProcessName", value: VersionInformation.Product)
                                        .WriteTo.Console()
                                        .CreateLogger();
    }
}