using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;
using Microsoft.Extensions.Logging;
using IProjectLoader = FunFair.BuildCheck.Interfaces.IProjectLoader;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Build;

internal static class DotNetBuild
{
    // MSB3243 - two assemblies of the same name, but different version
    // NU1802 - restoring from HTTP source
    private const string NO_WARN = "-p:NoWarn=NU1802";
    private const string BUILD_VERSION = "0.0.0.1-do-not-distribute";

    public static BuildSettings LoadBuildSettings(IProjectLoader projectLoader, IReadOnlyList<string> projects)
    {
        bool packable = false;
        bool publishable = false;
        string? framework = null;

        foreach (string project in projects)
        {
            XmlDocument doc = projectLoader.Load(project);

            XmlNode? outputTypeNode = doc.SelectSingleNode("/Project/PropertyGroup/OutputType");

            if (outputTypeNode is not null)
            {
                if (!packable)
                {
                    packable |= IsPackable(doc: doc, outputType: outputTypeNode.InnerText);
                }

                string? candidateFramework = GetTargetFrameworks(doc: doc, outputType: outputTypeNode.InnerText)
                    .MaxBy(keySelector: x => x, comparer: StringComparer.OrdinalIgnoreCase);

                if (candidateFramework is not null)
                {
                    publishable = true;

                    if (framework is null)
                    {
                        framework = candidateFramework;
                    }
                    else
                    {
                        if (StringComparer.OrdinalIgnoreCase.Compare(x: candidateFramework, y: framework) > 0)
                        {
                            framework = candidateFramework;
                        }
                    }
                }
            }
        }

        return new(Publishable: publishable, Packable: packable, Framework: framework);
    }

    private static IReadOnlyList<string> GetTargetFrameworks(XmlDocument doc, string outputType)
    {
        if (!StringComparer.InvariantCultureIgnoreCase.Equals(x: outputType, y: "Exe"))
        {
            return Array.Empty<string>();
        }

        XmlNode? publishableNode = doc.SelectSingleNode("/Project/PropertyGroup/IsPublishable");

        if (publishableNode is not null && StringComparer.InvariantCultureIgnoreCase.Equals(x: publishableNode.InnerText, y: "True"))
        {
            XmlNode? targetFrameworkNode = doc.SelectSingleNode("/Project/PropertyGroup/TargetFramework");

            if (targetFrameworkNode is not null)
            {
                return [targetFrameworkNode.InnerText];
            }

            XmlNode? targetFrameworksNode = doc.SelectSingleNode("/Project/PropertyGroup/TargetFrameworks");

            if (targetFrameworksNode is not null)
            {
                return targetFrameworksNode.InnerText.Split(';');
            }
        }

        return Array.Empty<string>();
    }

    private static bool IsPackable(XmlDocument doc, string outputType)
    {
        if (!StringComparer.InvariantCultureIgnoreCase.Equals(x: outputType, y: "Library"))
        {
            return false;
        }

        XmlNode? packableNode = doc.SelectSingleNode("/Project/PropertyGroup/IsPackable");

        return packableNode is not null && StringComparer.InvariantCultureIgnoreCase.Equals(x: packableNode.InnerText, y: "True");
    }

    public static async ValueTask BuildAsync(string basePath, BuildSettings buildSettings, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Building {basePath}...");

        await StopBuildServerAsync(basePath: basePath, logger: logger, cancellationToken: cancellationToken);

        try
        {
            await DotNetCleanAsync(basePath: basePath, logger: logger, cancellationToken: cancellationToken);

            await DotNetRestoreAsync(basePath: basePath, logger: logger, cancellationToken: cancellationToken);

            await DotNetBuildAsync(basePath: basePath, logger: logger, cancellationToken: cancellationToken);

            await DotNetTestAsync(basePath: basePath, logger: logger, cancellationToken: cancellationToken);

            if (buildSettings.Packable)
            {
                await DotNetPackAsync(basePath: basePath, logger: logger, cancellationToken: cancellationToken);
            }

            if (buildSettings.Publishable)
            {
                string? framework = buildSettings.Framework;
                await DotNetPublishAsync(basePath: basePath, logger: logger, cancellationToken: cancellationToken, framework: framework);
            }
        }
        finally
        {
            await StopBuildServerAsync(basePath: basePath, logger: logger, cancellationToken: cancellationToken);
        }
    }

    private static async ValueTask DotNetPublishAsync(string basePath, string? framework, ILogger logger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(framework))
        {
            logger.LogInformation("Publishing no framework...");
            await ExecRequireCleanAsync(basePath: basePath,
                                        $"publish -warnaserror -p:PublishSingleFile=true --configuration:Releases -r:linux-x64 --self-contained -p:PublishReadyToRun=False -p:PublishReadyToRunShowWarnings=True -p:PublishTrimmed=False -p:DisableSwagger=False -p:TreatWarningsAsErrors=True -p:Version={BUILD_VERSION} -p:IncludeNativeLibrariesForSelfExtract=false -nodeReuse:False {NO_WARN}",
                                        logger: logger,
                                        cancellationToken: cancellationToken);
        }
        else
        {
            logger.LogInformation($"Publishing {framework}...");
            await ExecRequireCleanAsync(basePath: basePath,
                                        $"publish -warnaserror -p:PublishSingleFile=true --configuration:Releases -r:linux-x64 --framework:{framework} --self-contained -p:PublishReadyToRun=False -p:PublishReadyToRunShowWarnings=True -p:PublishTrimmed=False -p:DisableSwagger=False -p:TreatWarningsAsErrors=True -p:Version={BUILD_VERSION} -p:IncludeNativeLibrariesForSelfExtract=false -nodeReuse:False {NO_WARN}",
                                        logger: logger,
                                        cancellationToken: cancellationToken);
        }
    }

    private static ValueTask DotNetPackAsync(string basePath, ILogger logger, in CancellationToken cancellationToken)
    {
        logger.LogInformation("Packing...");

        return ExecRequireCleanAsync(basePath: basePath,
                                     $"pack --no-restore -nodeReuse:False --configuration=Releases -p:Version={BUILD_VERSION} {NO_WARN}",
                                     logger: logger,
                                     cancellationToken: cancellationToken);
    }

    private static ValueTask DotNetTestAsync(string basePath, ILogger logger, in CancellationToken cancellationToken)
    {
        logger.LogInformation("Testing...");

        return ExecRequireCleanAsync(basePath: basePath,
                                     $"test --no-build --no-restore -nodeReuse:False --configuration Releases -p:Version={BUILD_VERSION} --filter FullyQualifiedName\\!~Integration {NO_WARN}",
                                     logger: logger,
                                     cancellationToken: cancellationToken);
    }

    private static ValueTask DotNetBuildAsync(string basePath, ILogger logger, in CancellationToken cancellationToken)
    {
        logger.LogInformation("Building...");

        return ExecRequireCleanAsync(basePath: basePath,
                                     $"build --no-restore -warnAsError -nodeReuse:False --configuration=Releases -p:Version={BUILD_VERSION} {NO_WARN}",
                                     logger: logger,
                                     cancellationToken: cancellationToken);
    }

    private static ValueTask DotNetRestoreAsync(string basePath, ILogger logger, in CancellationToken cancellationToken)
    {
        logger.LogInformation("Restoring...");

        return ExecRequireCleanAsync(basePath: basePath, $"restore -nodeReuse:False -r:linux-x64 {NO_WARN}", logger: logger, cancellationToken: cancellationToken);
    }

    private static ValueTask DotNetCleanAsync(string basePath, ILogger logger, in CancellationToken cancellationToken)
    {
        logger.LogInformation("Cleaning...");

        return ExecRequireCleanAsync(basePath: basePath, $"clean --configuration=Releases -nodeReuse:False {NO_WARN}", logger: logger, cancellationToken: cancellationToken);
    }

    private static async Task StopBuildServerAsync(string basePath, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping build server...");
        await ExecAsync(basePath: basePath, arguments: "build-server shutdown", cancellationToken: cancellationToken);
    }

    private static async ValueTask ExecRequireCleanAsync(string basePath, string arguments, ILogger logger, CancellationToken cancellationToken)
    {
        (string[] results, int exitCode) = await ExecAsync(basePath: basePath, arguments: arguments, cancellationToken: cancellationToken);

        if (exitCode != 0)
        {
            DumpErrors(results: results, logger: logger);

            throw new DotNetBuildErrorException();
        }
    }

    private static void DumpErrors(string[] results, ILogger logger)
    {
        foreach (string line in results)
        {
            logger.LogError(line);
        }
    }

    private static async ValueTask<(string[] Output, int ExitCode)> ExecAsync(string basePath, string arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo psi = new()
                               {
                                   FileName = "dotnet",
                                   WorkingDirectory = basePath,
                                   Arguments = arguments,
                                   RedirectStandardOutput = true,
                                   RedirectStandardError = true,
                                   UseShellExecute = false,
                                   CreateNoWindow = true
                               };

        using (Process? process = Process.Start(psi))
        {
            if (process is null)
            {
                throw new InvalidOperationException("Failed to start git");
            }

#if NET7_0_OR_GREATER
            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);

            // string error = await process.StandardError.ReadToEndAsync(cancellationToken);
#else
            string output = await process.StandardOutput.ReadToEndAsync();

            // string error = await process.StandardError.ReadToEndAsync();
#endif

            await process.WaitForExitAsync(cancellationToken);

            return (output.Split(separator: Environment.NewLine, options: StringSplitOptions.RemoveEmptyEntries), process.ExitCode);
        }
    }
}