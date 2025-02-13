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
    private const string BUILD_VERSION = "0.0.0.1-do-not-distribute";

    private static readonly IReadOnlyList<string> NoWarnAll =
    [
        // MSB3243 - two assemblies of the same name, but different version
        // NU1802 - restoring from HTTP source
        "NU1802",
    ];

    private static readonly IReadOnlyList<string> NoWarnPreRelease =
    [
        // NU1901 - Package with low severity detected
        "NU1901",
        // NU1902 -Package with moderate severity detected
        "NU1902",
        // NU1903 - Package with high severity detected
        "NU1903",
        // NU1904 - Package with critical severity detected
        "NU1904",
    ];

    private readonly ILogger<DotNetBuild> _logger;
    private readonly IProjectXmlLoader _projectLoader;

    public DotNetBuild(ILogger<DotNetBuild> logger)
    {
        this._logger = logger;

        this._projectLoader = new ProjectXmlLoader();
    }

    public async ValueTask BuildAsync(
        string basePath,
        BuildSettings buildSettings,
        BuildOverride buildOverride,
        CancellationToken cancellationToken
    )
    {
        this._logger.LogStartingBuild(basePath);

        await this.StopBuildServerAsync(basePath: basePath, cancellationToken: cancellationToken);

        try
        {
            await this.DotNetCleanAsync(
                basePath: basePath,
                buildOverride: buildOverride,
                cancellationToken: cancellationToken
            );

            await this.DotNetRestoreAsync(
                basePath: basePath,
                buildOverride: buildOverride,
                cancellationToken: cancellationToken
            );

            await this.DotNetBuildAsync(
                basePath: basePath,
                buildOverride: buildOverride,
                cancellationToken: cancellationToken
            );

            await this.DotNetTestAsync(
                basePath: basePath,
                buildOverride: buildOverride,
                cancellationToken: cancellationToken
            );

            if (buildSettings.Packable)
            {
                await this.DotNetPackAsync(
                    basePath: basePath,
                    buildOverride: buildOverride,
                    cancellationToken: cancellationToken
                );
            }

            if (buildSettings.Publishable)
            {
                string? framework = buildSettings.Framework;
                await this.DotNetPublishAsync(
                    basePath: basePath,
                    buildOverride: buildOverride,
                    framework: framework,
                    cancellationToken: cancellationToken
                );
            }
        }
        finally
        {
            await this.StopBuildServerAsync(
                basePath: basePath,
                cancellationToken: cancellationToken
            );
        }
    }

    public async ValueTask<BuildSettings> LoadBuildSettingsAsync(
        IReadOnlyList<string> projects,
        CancellationToken cancellationToken
    )
    {
        bool packable = false;
        bool publishable = false;
        string? framework = null;

        foreach (string project in projects)
        {
            XmlDocument doc = await this._projectLoader.LoadAsync(
                path: project,
                cancellationToken: cancellationToken
            );

            this.CheckProjectSettings(
                doc: doc,
                packable: ref packable,
                publishable: ref publishable,
                framework: ref framework
            );
        }

        return new(Publishable: publishable, Packable: packable, Framework: framework);
    }

    private void CheckProjectSettings(
        XmlDocument doc,
        ref bool packable,
        ref bool publishable,
        ref string? framework
    )
    {
        XmlNode? outputTypeNode = doc.SelectSingleNode("/Project/PropertyGroup/OutputType");

        if (outputTypeNode is null)
        {
            return;
        }

        packable |= IsPackable(doc: doc, outputType: outputTypeNode.InnerText);

        string? candidateFramework = GetTargetFrameworks(
                doc: doc,
                outputType: outputTypeNode.InnerText
            )
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
                if (
                    StringComparer.OrdinalIgnoreCase.Compare(x: candidateFramework, y: framework)
                    > 0
                )
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
            return [];
        }

        XmlNode? publishableNode = doc.SelectSingleNode("/Project/PropertyGroup/IsPublishable");

        if (
            publishableNode is not null
            && StringComparer.InvariantCultureIgnoreCase.Equals(
                x: publishableNode.InnerText,
                y: "True"
            )
        )
        {
            XmlNode? targetFrameworkNode = doc.SelectSingleNode(
                "/Project/PropertyGroup/TargetFramework"
            );

            if (targetFrameworkNode is not null)
            {
                return [targetFrameworkNode.InnerText];
            }

            XmlNode? targetFrameworksNode = doc.SelectSingleNode(
                "/Project/PropertyGroup/TargetFrameworks"
            );

            if (targetFrameworksNode is not null)
            {
                return targetFrameworksNode.InnerText.Split(
                    separator: ';',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                );
            }
        }

        return [];
    }

    private static bool IsPackable(XmlDocument doc, string outputType)
    {
        if (!StringComparer.InvariantCultureIgnoreCase.Equals(x: outputType, y: "Library"))
        {
            return false;
        }

        XmlNode? packableNode = doc.SelectSingleNode("/Project/PropertyGroup/IsPackable");

        return packableNode is not null
            && StringComparer.InvariantCultureIgnoreCase.Equals(
                x: packableNode.InnerText,
                y: "True"
            );
    }

    private async ValueTask DotNetPublishAsync(
        string basePath,
        BuildOverride buildOverride,
        string? framework,
        CancellationToken cancellationToken
    )
    {
        string noWarn = BuildNoWarn(buildOverride);

        if (string.IsNullOrWhiteSpace(framework))
        {
            this._logger.LogPublishingNoFramework();
            await this.ExecRequireCleanAsync(
                basePath: basePath,
                $"publish -warnaserror -p:PublishSingleFile=true --configuration:Release -r:linux-x64 --self-contained -p:CSharpier_Check:true -p:PublishReadyToRun=False -p:PublishReadyToRunShowWarnings=True -p:PublishTrimmed=False -p:DisableSwagger=False -p:TreatWarningsAsErrors=True -p:Version={BUILD_VERSION} -p:IncludeNativeLibrariesForSelfExtract=false -nodeReuse:False {noWarn}",
                cancellationToken: cancellationToken
            );
        }
        else
        {
            this._logger.LogPublishingWithFramework(framework);
            await this.ExecRequireCleanAsync(
                basePath: basePath,
                $"publish -warnaserror -p:PublishSingleFile=true --configuration:Release -r:linux-x64 --framework:{framework} --self-contained -p:CSharpier_Check:true -p:PublishReadyToRun=False -p:PublishReadyToRunShowWarnings=True -p:PublishTrimmed=False -p:DisableSwagger=False -p:TreatWarningsAsErrors=True -p:Version={BUILD_VERSION} -p:IncludeNativeLibrariesForSelfExtract=false -nodeReuse:False {noWarn}",
                cancellationToken: cancellationToken
            );
        }
    }

    private static string BuildNoWarn(in BuildOverride buildOverride)
    {
        if (buildOverride.PreRelease)
        {
            return Build([.. NoWarnPreRelease.Concat(NoWarnAll).Distinct(StringComparer.Ordinal)]);
        }

        return Build(NoWarnAll);

        static string Build(IReadOnlyList<string> items)
        {
            if (items is [])
            {
                return string.Empty;
            }

            string quotedPropertyEscape = OperatingSystem.IsLinux() ? "'" : "\\";

            return $"-nowarn:{quotedPropertyEscape}\"{string.Join(separator: ';', items.Order(comparer: StringComparer.Ordinal))}{quotedPropertyEscape}\"";
        }
    }

    private ValueTask DotNetPackAsync(
        string basePath,
        in BuildOverride buildOverride,
        in CancellationToken cancellationToken
    )
    {
        string noWarn = BuildNoWarn(buildOverride);

        this._logger.LogPacking();

        return this.ExecRequireCleanAsync(
            basePath: basePath,
            $"pack --no-restore -nodeReuse:False --configuration:Release -p:Version={BUILD_VERSION} -p:CSharpier_Check:true  {noWarn}",
            cancellationToken: cancellationToken
        );
    }

    private ValueTask DotNetTestAsync(
        string basePath,
        in BuildOverride buildOverride,
        in CancellationToken cancellationToken
    )
    {
        string noWarn = BuildNoWarn(buildOverride);

        this._logger.LogTesting();

        return this.ExecRequireCleanAsync(
            basePath: basePath,
            $"test --no-build --no-restore -nodeReuse:False --configuration:Release -p:Version={BUILD_VERSION} -p:CSharpier_Check:true --filter FullyQualifiedName\\!~Integration {noWarn}",
            cancellationToken: cancellationToken
        );
    }

    private ValueTask DotNetBuildAsync(
        string basePath,
        in BuildOverride buildOverride,
        in CancellationToken cancellationToken
    )
    {
        string noWarn = BuildNoWarn(buildOverride);

        this._logger.LogBuilding();

        return this.ExecRequireCleanAsync(
            basePath: basePath,
            $"build --no-restore -warnAsError -nodeReuse:False --configuration:Release -p:Version={BUILD_VERSION} -p:CSharpier_Check:true {noWarn}",
            cancellationToken: cancellationToken
        );
    }

    private ValueTask DotNetRestoreAsync(
        string basePath,
        in BuildOverride buildOverride,
        in CancellationToken cancellationToken
    )
    {
        string noWarn = BuildNoWarn(buildOverride);

        this._logger.LogRestoring();

        return this.ExecRequireCleanAsync(
            basePath: basePath,
            $"restore -nodeReuse:False -r:linux-x64 {noWarn}",
            cancellationToken: cancellationToken
        );
    }

    private ValueTask DotNetCleanAsync(
        string basePath,
        in BuildOverride buildOverride,
        in CancellationToken cancellationToken
    )
    {
        string noWarn = BuildNoWarn(buildOverride);

        this._logger.LogCleaning();

        return this.ExecRequireCleanAsync(
            basePath: basePath,
            $"clean --configuration:Release -nodeReuse:False {noWarn}",
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask StopBuildServerAsync(
        string basePath,
        CancellationToken cancellationToken
    )
    {
        this._logger.LogStoppingBuildServer();
        await ExecAsync(
            basePath: basePath,
            arguments: "build-server shutdown",
            cancellationToken: cancellationToken
        );
        this._logger.LogStoppedBuildServer();
    }

    private async ValueTask ExecRequireCleanAsync(
        string basePath,
        string arguments,
        CancellationToken cancellationToken
    )
    {
        (string[] results, int exitCode) = await ExecAsync(
            basePath: basePath,
            arguments: arguments,
            cancellationToken: cancellationToken
        );

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

    private static async ValueTask<(string[] Output, int ExitCode)> ExecAsync(
        string basePath,
        string arguments,
        CancellationToken cancellationToken
    )
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
                ["MSBUILDTERMINALLOGGER"] = "false",
            },
        };

        using (Process? process = Process.Start(psi))
        {
            if (process is null)
            {
                throw new InvalidOperationException("Failed to start dotnet");
            }

#if NET7_0_OR_GREATER
            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);

            string error = await process.StandardError.ReadToEndAsync(cancellationToken);
#else
            string output = await process.StandardOutput.ReadToEndAsync();

            string error = await process.StandardError.ReadToEndAsync();
#endif

            await process.WaitForExitAsync(cancellationToken);

            string result = string.Join(separator: Environment.NewLine, output, error);

            return (
                result.Split(
                    separator: Environment.NewLine,
                    options: StringSplitOptions.RemoveEmptyEntries
                ),
                process.ExitCode
            );
        }
    }
}
