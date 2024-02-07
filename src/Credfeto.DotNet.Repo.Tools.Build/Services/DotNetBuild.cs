using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Build.Services.LoggingExtensions;
using FunFair.BuildCheck.Interfaces;
using FunFair.BuildCheck.Runner.Services;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Build.Services;

public sealed class DotNetBuild : IDotNetBuild
{
    // MSB3243 - two assemblies of the same name, but different version
    // NU1802 - restoring from HTTP source
    private const string NO_WARN = "-p:NoWarn=NU1802";
    private const string BUILD_VERSION = "0.0.0.1-do-not-distribute";
    private readonly ILogger<DotNetBuild> _logger;
    private readonly IProjectXmlLoader _projectLoader;

    public DotNetBuild(ILogger<DotNetBuild> logger)
    {
        this._logger = logger;

        this._projectLoader = new ProjectXmlLoader();
    }

    public async ValueTask BuildAsync(string basePath, BuildSettings buildSettings, CancellationToken cancellationToken)
    {
        this._logger.LogStartingBuild(basePath);

        await this.StopBuildServerAsync(basePath: basePath, cancellationToken: cancellationToken);

        try
        {
            await this.DotNetCleanAsync(basePath: basePath, cancellationToken: cancellationToken);

            await this.DotNetRestoreAsync(basePath: basePath, cancellationToken: cancellationToken);

            await this.DotNetBuildAsync(basePath: basePath, cancellationToken: cancellationToken);

            await this.DotNetTestAsync(basePath: basePath, cancellationToken: cancellationToken);

            if (buildSettings.Packable)
            {
                await this.DotNetPackAsync(basePath: basePath, cancellationToken: cancellationToken);
            }

            if (buildSettings.Publishable)
            {
                string? framework = buildSettings.Framework;
                await this.DotNetPublishAsync(basePath: basePath, cancellationToken: cancellationToken, framework: framework);
            }
        }
        finally
        {
            await this.StopBuildServerAsync(basePath: basePath, cancellationToken: cancellationToken);
        }
    }

    public async ValueTask<BuildSettings> LoadBuildSettingsAsync(IReadOnlyList<string> projects, CancellationToken cancellationToken)
    {
        bool packable = false;
        bool publishable = false;
        string? framework = null;

        foreach (string project in projects)
        {
            XmlDocument doc = await this._projectLoader.LoadAsync(path: project, cancellationToken: cancellationToken);

            this.CheckProjectSettings(doc: doc, packable: ref packable, publishable: ref publishable, framework: ref framework);
        }

        return new(Publishable: publishable, Packable: packable, Framework: framework);
    }

    private void CheckProjectSettings(XmlDocument doc, ref bool packable, ref bool publishable, ref string? framework)
    {
        XmlNode? outputTypeNode = doc.SelectSingleNode("/Project/PropertyGroup/OutputType");

        if (outputTypeNode is null)
        {
            return;
        }

        packable |= IsPackable(doc: doc, outputType: outputTypeNode.InnerText);

        string? candidateFramework = GetTargetFrameworks(doc: doc, outputType: outputTypeNode.InnerText)
            .MaxBy(keySelector: x => x, comparer: StringComparer.OrdinalIgnoreCase);

        if (candidateFramework is not null)
        {
            publishable = true;

            if (framework is null)
            {
                framework = candidateFramework;
                this._logger.LogFoundFramework(framework);
            }
            else
            {
                if (StringComparer.OrdinalIgnoreCase.Compare(x: candidateFramework, y: framework) > 0)
                {
                    framework = candidateFramework;
                    this._logger.LogFoundFramework(framework);
                }
            }
        }
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

    private async ValueTask DotNetPublishAsync(string basePath, string? framework, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(framework))
        {
            this._logger.LogPublishingNoFramework();
            await this.ExecRequireCleanAsync(basePath: basePath,
                                             $"publish -warnaserror -p:PublishSingleFile=true --configuration:Release -r:linux-x64 --self-contained -p:PublishReadyToRun=False -p:PublishReadyToRunShowWarnings=True -p:PublishTrimmed=False -p:DisableSwagger=False -p:TreatWarningsAsErrors=True -p:Version={BUILD_VERSION} -p:IncludeNativeLibrariesForSelfExtract=false -nodeReuse:False {NO_WARN}",
                                             cancellationToken: cancellationToken);
        }
        else
        {
            this._logger.LogPublishingWithFramework(framework);
            await this.ExecRequireCleanAsync(basePath: basePath,
                                             $"publish -warnaserror -p:PublishSingleFile=true --configuration:Release -r:linux-x64 --framework:{framework} --self-contained -p:PublishReadyToRun=False -p:PublishReadyToRunShowWarnings=True -p:PublishTrimmed=False -p:DisableSwagger=False -p:TreatWarningsAsErrors=True -p:Version={BUILD_VERSION} -p:IncludeNativeLibrariesForSelfExtract=false -nodeReuse:False {NO_WARN}",
                                             cancellationToken: cancellationToken);
        }
    }

    private ValueTask DotNetPackAsync(string basePath, in CancellationToken cancellationToken)
    {
        this._logger.LogPacking();

        return this.ExecRequireCleanAsync(basePath: basePath, $"pack --no-restore -nodeReuse:False --configuration:Release -p:Version={BUILD_VERSION} {NO_WARN}", cancellationToken: cancellationToken);
    }

    private ValueTask DotNetTestAsync(string basePath, in CancellationToken cancellationToken)
    {
        this._logger.LogTesting();

        return this.ExecRequireCleanAsync(basePath: basePath,
                                          $"test --no-build --no-restore -nodeReuse:False --configuration:Release -p:Version={BUILD_VERSION} --filter FullyQualifiedName\\!~Integration {NO_WARN}",
                                          cancellationToken: cancellationToken);
    }

    private ValueTask DotNetBuildAsync(string basePath, in CancellationToken cancellationToken)
    {
        this._logger.LogBuilding();

        return this.ExecRequireCleanAsync(basePath: basePath,
                                          $"build --no-restore -warnAsError -nodeReuse:False --configuration:Release -p:Version={BUILD_VERSION} {NO_WARN}",
                                          cancellationToken: cancellationToken);
    }

    private ValueTask DotNetRestoreAsync(string basePath, in CancellationToken cancellationToken)
    {
        this._logger.LogRestoring();

        return this.ExecRequireCleanAsync(basePath: basePath, $"restore -nodeReuse:False -r:linux-x64 {NO_WARN}", cancellationToken: cancellationToken);
    }

    private ValueTask DotNetCleanAsync(string basePath, in CancellationToken cancellationToken)
    {
        this._logger.LogCleaning();

        return this.ExecRequireCleanAsync(basePath: basePath, $"clean --configuration:Release -nodeReuse:False {NO_WARN}", cancellationToken: cancellationToken);
    }

    private async ValueTask StopBuildServerAsync(string basePath, CancellationToken cancellationToken)
    {
        this._logger.LogStoppingBuildServer();
        await ExecAsync(basePath: basePath, arguments: "build-server shutdown", cancellationToken: cancellationToken);
        this._logger.LogStoppedBuildServer();
    }

    private async ValueTask ExecRequireCleanAsync(string basePath, string arguments, CancellationToken cancellationToken)
    {
        (string[] results, int exitCode) = await ExecAsync(basePath: basePath, arguments: arguments, cancellationToken: cancellationToken);

        if (exitCode != 0)
        {
            this.DumpErrors(results: results);

            throw new DotNetBuildErrorException();
        }
    }

    private void DumpErrors(string[] results)
    {
        if (!this._logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        foreach (string line in results)
        {
            this._logger.LogError(line);
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
                                   CreateNoWindow = true,
                                   Environment =
                                   {
                                       ["DOTNET_NOLOGO"] = "true",
                                       ["DOTNET_PRINT_TELEMETRY_MESSAGE"] = "0",
                                       ["DOTNET_ReadyToRun"] = "0",
                                       ["DOTNET_TC_QuickJitForLoops"] = "1",
                                       ["DOTNET_TieredPGO"] = "1",
                                       ["MSBUILDTERMINALLOGGER"] = "false"
                                   }
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