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
        // NU1902 - Package with moderate severity detected
        "NU1902",
        // NU1903 - Package with high severity detected
        "NU1903",
        // NU1904 - Package with critical severity detected
        "NU1904",
    ];

    private readonly ILogger<DotNetBuild> _logger;
    private readonly IProjectXmlLoader _projectLoader;

    public DotNetBuild(IProjectXmlLoader projectLoader, ILogger<DotNetBuild> logger)
    {
        this._logger = logger;

        this._projectLoader = projectLoader;
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
            await this.StopBuildServerAsync(basePath: basePath, cancellationToken: cancellationToken);
        }
    }

    public async ValueTask<BuildSettings> LoadBuildSettingsAsync(
        IReadOnlyList<string> projects,
        CancellationToken cancellationToken
    )
    {
        List<string> packable = [];
        List<string> publishable = [];
        string? framework = null;

        foreach (string project in projects)
        {
            XmlDocument doc = await this._projectLoader.LoadAsync(path: project, cancellationToken: cancellationToken);

            this.CheckProjectSettings(
                project: project,
                doc: doc,
                packable: packable,
                publishable: publishable,
                framework: ref framework
            );
        }

        return new([.. publishable], [.. packable], Framework: framework);
    }

    private void CheckProjectSettings(
        string project,
        XmlDocument doc,
        List<string> packable,
        List<string> publishable,
        ref string? framework
    )
    {
        XmlNode? outputTypeNode = doc.SelectSingleNode("/Project/PropertyGroup/OutputType");

        if (outputTypeNode is null)
        {
            this._logger.LogProjectHasNoOutputType(project);

            return;
        }

        if (IsPackable(doc: doc, outputType: outputTypeNode.InnerText))
        {
            this._logger.LogProjectIsPackable(project);
            packable.Add(project);
        }

        if (IsPublishable(doc: doc, outputType: outputTypeNode.InnerText))
        {
            this._logger.LogProjectIsPublishable(project);
            publishable.Add(project);

            string? candidateFramework = GetTargetFrameworks(doc: doc)
                .MaxBy(keySelector: x => x, comparer: StringComparer.OrdinalIgnoreCase);

            if (candidateFramework is null)
            {
                return;
            }

            if (framework is null || StringComparer.OrdinalIgnoreCase.Compare(x: candidateFramework, y: framework) > 0)
            {
                framework = candidateFramework;
                this._logger.LogFoundFramework(framework);
            }
        }
    }

    private static IReadOnlyList<string> GetTargetFrameworks(XmlDocument doc)
    {
        XmlNode? targetFrameworkNode = doc.SelectSingleNode("/Project/PropertyGroup/TargetFramework");

        if (targetFrameworkNode is not null)
        {
            return [targetFrameworkNode.InnerText];
        }

        XmlNode? targetFrameworksNode = doc.SelectSingleNode("/Project/PropertyGroup/TargetFrameworks");

        return targetFrameworksNode is not null
            ? targetFrameworksNode.InnerText.Split(
                separator: ';',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
            )
            : [];
    }

    private static bool IsPackable(XmlDocument doc, string outputType)
    {
        if (!IsLibrary(outputType))
        {
            if (!IsDotNetTool(doc))
            {
                return false;
            }
        }

        return IsBooleanDocPropertyTrue(doc: doc, path: "/Project/PropertyGroup/IsPackable");
    }

    private static bool IsBooleanDocPropertyTrue(XmlDocument doc, string path)
    {
        XmlNode? packableNode = doc.SelectSingleNode(path);

        return packableNode is not null
            && StringComparer.OrdinalIgnoreCase.Equals(x: packableNode.InnerText, y: "True");
    }

    private static bool IsDotNetTool(XmlDocument doc)
    {
        return IsBooleanDocPropertyTrue(doc: doc, path: "/Project/PropertyGroup/PackAsTool");
    }

    private static bool IsLibrary(string outputType)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(x: outputType, y: "Library");
    }

    private static bool IsPublishable(XmlDocument doc, string outputType)
    {
        if (IsExe(outputType))
        {
            return IsBooleanDocPropertyTrue(doc: doc, path: "/Project/PropertyGroup/IsPublishable");
        }

        return false;
    }

    private static bool IsExe(string outputType)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(x: outputType, y: "Exe");
    }

    private static string EnvironmentParameter((string name, string value) p)
    {
        return EnvironmentParameter(name: p.name, value: p.value);
    }

    private static string EnvironmentParameter(string name, string value)
    {
        return "-p:" + name + "=" + value;
    }

    private static string BuildEnvironmentParameters(params (string name, string value)[] parameters)
    {
        if (parameters.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(separator: ' ', parameters.Select(EnvironmentParameter));
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
            string parameters = BuildEnvironmentParameters(
                ("DisableSwagger", "False"),
                ("IncludeNativeLibrariesForSelfExtract", "false"),
                ("PublishReadyToRun", "False"),
                ("PublishReadyToRunShowWarnings", "True"),
                ("PublishSingleFile", "true"),
                ("PublishTrimmed", "False"),
                ("TreatWarningsAsErrors", "True"),
                ("Version", BUILD_VERSION)
            );

            this._logger.LogPublishingNoFramework();
            await this.ExecRequireCleanAsync(
                basePath: basePath,
                $"publish -warnaserror  --configuration:Release -r:linux-x64 --self-contained {parameters} -nodeReuse:False {noWarn}",
                cancellationToken: cancellationToken
            );
        }
        else
        {
            string parameters = BuildEnvironmentParameters(
                ("DisableSwagger", "False"),
                ("IncludeNativeLibrariesForSelfExtract", "false"),
                ("PublishReadyToRun", "False"),
                ("PublishReadyToRunShowWarnings", "True"),
                ("PublishSingleFile", "true"),
                ("PublishTrimmed", "False"),
                ("TreatWarningsAsErrors", "True"),
                ("Version", BUILD_VERSION)
            );
            this._logger.LogPublishingWithFramework(framework);
            await this.ExecRequireCleanAsync(
                basePath: basePath,
                $"publish -warnaserror --configuration:Release -r:linux-x64 --framework:{framework} --self-contained {parameters} -nodeReuse:False {noWarn}",
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

        string parameters = BuildEnvironmentParameters(("Version", BUILD_VERSION));

        this._logger.LogPacking();

        return this.ExecRequireCleanAsync(
            basePath: basePath,
            $"pack --no-restore -nodeReuse:False --configuration:Release {parameters} {noWarn}",
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
        string parameters = BuildEnvironmentParameters(("Version", BUILD_VERSION));

        this._logger.LogTesting();

        return this.ExecRequireCleanAsync(
            basePath: basePath,
            $"test --no-build --no-restore -nodeReuse:False --configuration:Release {parameters} --filter FullyQualifiedName\\!~Integration {noWarn}",
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
        string parameters = BuildEnvironmentParameters(("Version", BUILD_VERSION));

        this._logger.LogBuilding();

        return this.ExecRequireCleanAsync(
            basePath: basePath,
            $"build --no-restore -warnAsError -nodeReuse:False --configuration:Release {parameters} {noWarn}",
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

    private async ValueTask StopBuildServerAsync(string basePath, CancellationToken cancellationToken)
    {
        this._logger.LogStoppingBuildServer();
        await ExecAsync(basePath: basePath, arguments: "build-server shutdown", cancellationToken: cancellationToken);
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
            this._logger.LogBuildError(line);
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
                result.Split(separator: Environment.NewLine, options: StringSplitOptions.RemoveEmptyEntries),
                process.ExitCode
            );
        }
    }
}
