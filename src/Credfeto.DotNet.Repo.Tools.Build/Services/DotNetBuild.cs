using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.DotNet.Code.Analysis.Overrides;
using Credfeto.DotNet.Code.Analysis.Overrides.Helpers;
using Credfeto.DotNet.Code.Analysis.Overrides.Models;
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

    public async ValueTask BuildAsync(BuildContext buildContext, CancellationToken cancellationToken)
    {
        this._logger.LogStartingBuild(buildContext.SourceDirectory);

        await this.StopBuildServerAsync(buildContext: buildContext, cancellationToken: cancellationToken);

        await using (await this.SetCodeAnalysisConfigAsync(buildContext: buildContext, cancellationToken: cancellationToken))
        {
            try
            {
                await this.DotNetCleanAsync(buildContext: buildContext, cancellationToken: cancellationToken);

                await this.DotNetRestoreAsync(buildContext: buildContext, cancellationToken: cancellationToken);

                await this.DotNetBuildAsync(buildContext: buildContext, cancellationToken: cancellationToken);

                await this.DotNetTestAsync(buildContext: buildContext, cancellationToken: cancellationToken);

                if (buildContext.BuildSettings.Packable)
                {
                    await this.DotNetPackAsync(buildContext: buildContext, cancellationToken: cancellationToken);
                }

                if (buildContext.BuildSettings.Publishable)
                {
                    string? framework = buildContext.BuildSettings.Framework;
                    await this.DotNetPublishAsync(buildContext: buildContext, framework: framework, cancellationToken: cancellationToken);
                }
            }
            finally
            {
                await this.StopBuildServerAsync(buildContext: buildContext, cancellationToken: cancellationToken);
            }
        }
    }

    public async ValueTask BuildAsync(string projectFileName, BuildContext buildContext, CancellationToken cancellationToken)
    {
        this._logger.LogStartingBuild(buildContext.SourceDirectory);

        await this.StopBuildServerAsync(buildContext: buildContext, cancellationToken: cancellationToken);

        await using (await this.SetCodeAnalysisConfigAsync(buildContext: buildContext, cancellationToken: cancellationToken))
        {
            try
            {
                string noWarn = BuildNoWarn(buildContext);
                string parameters = BuildEnvironmentParameters(("Version", BUILD_VERSION),
                                                               ("SolutionDir", buildContext.SourceDirectory),
                                                               ("SuppressNETCoreSdkPreviewMessage", "True"),
                                                               ("Optimize", "True"),
                                                               ("ContinuousIntegrationBuild", "True"));

                this._logger.LogBuilding();

                await this.ExecRequireCleanAsync(buildContext: buildContext,
                                                 $"build  {projectFileName} -warnAsError -nodeReuse:False --configuration:Release {parameters} {noWarn}",
                                                 cancellationToken: cancellationToken);
            }
            finally
            {
                await this.StopBuildServerAsync(buildContext: buildContext, cancellationToken: cancellationToken);
            }
        }
    }

    public async ValueTask<BuildSettings> LoadBuildSettingsAsync(IReadOnlyList<string> projects, CancellationToken cancellationToken)
    {
        List<string> packable = [];
        List<string> publishable = [];
        string? framework = null;

        foreach (string project in projects)
        {
            XmlDocument doc = await this._projectLoader.LoadAsync(path: project, cancellationToken: cancellationToken);

            this.CheckProjectSettings(project: project, doc: doc, packable: packable, publishable: publishable, framework: ref framework);
        }

        return new([.. publishable], [.. packable], Framework: framework);
    }

    private void CheckProjectSettings(string project, XmlDocument doc, List<string> packable, List<string> publishable, ref string? framework)
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
            ? targetFrameworksNode.InnerText.Split(separator: ';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
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

        return packableNode is not null && StringComparer.OrdinalIgnoreCase.Equals(x: packableNode.InnerText, y: "True");
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

    private async ValueTask DotNetPublishAsync(BuildContext buildContext, string? framework, CancellationToken cancellationToken)
    {
        string noWarn = BuildNoWarn(buildContext);

        if (string.IsNullOrWhiteSpace(framework))
        {
            string parameters = BuildEnvironmentParameters(("DisableSwagger", "False"),
                                                           ("IncludeNativeLibrariesForSelfExtract", "false"),
                                                           ("PublishReadyToRun", "False"),
                                                           ("PublishReadyToRunShowWarnings", "True"),
                                                           ("PublishSingleFile", "true"),
                                                           ("PublishTrimmed", "False"),
                                                           ("TreatWarningsAsErrors", "True"),
                                                           ("Version", BUILD_VERSION));

            this._logger.LogPublishingNoFramework();
            await this.ExecRequireCleanAsync(buildContext: buildContext,
                                             $"publish -warnaserror  --configuration:Release -r:linux-x64 --self-contained {parameters} -nodeReuse:False {noWarn}",
                                             cancellationToken: cancellationToken);
        }
        else
        {
            string parameters = BuildEnvironmentParameters(("DisableSwagger", "False"),
                                                           ("IncludeNativeLibrariesForSelfExtract", "false"),
                                                           ("PublishReadyToRun", "False"),
                                                           ("PublishReadyToRunShowWarnings", "True"),
                                                           ("PublishSingleFile", "true"),
                                                           ("PublishTrimmed", "False"),
                                                           ("TreatWarningsAsErrors", "True"),
                                                           ("Version", BUILD_VERSION));
            this._logger.LogPublishingWithFramework(framework);
            await this.ExecRequireCleanAsync(buildContext: buildContext,
                                             $"publish -warnaserror --configuration:Release -r:linux-x64 --framework:{framework} --self-contained {parameters} -nodeReuse:False {noWarn}",
                                             cancellationToken: cancellationToken);
        }
    }

    private static string BuildNoWarn(in BuildContext buildContext)
    {
        if (buildContext.BuildOverride.PreRelease)
        {
            return Build([
                .. NoWarnPreRelease.Concat(NoWarnAll)
                                   .Distinct(StringComparer.Ordinal)
            ]);
        }

        return Build(NoWarnAll);

        static string Build(IReadOnlyList<string> items)
        {
            if (items is [])
            {
                return string.Empty;
            }

            string quotedPropertyEscape = OperatingSystem.IsLinux()
                ? "'"
                : "\\";

            return $"-nowarn:{quotedPropertyEscape}\"{string.Join(separator: ';', items.Order(comparer: StringComparer.Ordinal))}{quotedPropertyEscape}\"";
        }
    }

    private ValueTask DotNetPackAsync(in BuildContext buildContext, in CancellationToken cancellationToken)
    {
        string noWarn = BuildNoWarn(buildContext);

        string parameters = BuildEnvironmentParameters(("Version", BUILD_VERSION), ("SuppressNETCoreSdkPreviewMessage", "True"), ("Optimize", "True"), ("ContinuousIntegrationBuild", "True"));

        this._logger.LogPacking();

        return this.ExecRequireCleanAsync(buildContext: buildContext, $"pack --no-restore -nodeReuse:False --configuration:Release {parameters} {noWarn}", cancellationToken: cancellationToken);
    }

    private ValueTask DotNetTestAsync(in BuildContext buildContext, in CancellationToken cancellationToken)
    {

        string parameters = BuildEnvironmentParameters(("Version", BUILD_VERSION), ("SuppressNETCoreSdkPreviewMessage", "True"), ("Optimize", "True"), ("ContinuousIntegrationBuild", "True"));

        this._logger.LogTesting();

        return this.ExecRequireCleanAsync(buildContext: buildContext, $"test --no-build --no-restore --configuration:Release {parameters}", cancellationToken: cancellationToken);
    }

    private ValueTask DotNetBuildAsync(in BuildContext buildContext, in CancellationToken cancellationToken)
    {
        string noWarn = BuildNoWarn(buildContext);
        string parameters = BuildEnvironmentParameters(("Version", BUILD_VERSION), ("SuppressNETCoreSdkPreviewMessage", "True"), ("Optimize", "True"), ("ContinuousIntegrationBuild", "True"));

        this._logger.LogBuilding();

        return this.ExecRequireCleanAsync(buildContext: buildContext,
                                          $"build --no-restore -warnAsError -nodeReuse:False --configuration:Release {parameters} {noWarn}",
                                          cancellationToken: cancellationToken);
    }

    private ValueTask DotNetRestoreAsync(in BuildContext buildContext, in CancellationToken cancellationToken)
    {
        string noWarn = BuildNoWarn(buildContext);

        this._logger.LogRestoring();

        return this.ExecRequireCleanAsync(buildContext: buildContext, $"restore -nodeReuse:False -r:linux-x64 {noWarn}", cancellationToken: cancellationToken);
    }

    private ValueTask DotNetCleanAsync(in BuildContext buildContext, in CancellationToken cancellationToken)
    {
        string noWarn = BuildNoWarn(buildContext);

        this._logger.LogCleaning();

        return this.ExecRequireCleanAsync(buildContext: buildContext, $"clean --configuration:Release -nodeReuse:False {noWarn}", cancellationToken: cancellationToken);
    }

    private async ValueTask StopBuildServerAsync(BuildContext buildContext, CancellationToken cancellationToken)
    {
        this._logger.LogStoppingBuildServer();
        await ExecAsync(buildContext: buildContext, arguments: "build-server shutdown", cancellationToken: cancellationToken);
        this._logger.LogStoppedBuildServer();
    }

    private async ValueTask ExecRequireCleanAsync(BuildContext buildContext, string arguments, CancellationToken cancellationToken)
    {
        (string[] results, int exitCode) = await ExecAsync(buildContext: buildContext, arguments: arguments, cancellationToken: cancellationToken);

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

    private static async ValueTask<(string[] Output, int ExitCode)> ExecAsync(BuildContext buildContext, string arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo psi = new()
                               {
                                   FileName = "dotnet",
                                   WorkingDirectory = buildContext.SourceDirectory,
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

            return (result.Split(separator: Environment.NewLine, options: StringSplitOptions.RemoveEmptyEntries), process.ExitCode);
        }
    }

    private static bool TryGetCodeAnalysisFileName(in BuildContext buildContext, [NotNullWhen(true)] out string? fileName)
    {
        string rulesetFileName = Path.Combine(buildContext.SourceDirectory, "CodeAnalysis.ruleset");

        if (!File.Exists(rulesetFileName))
        {
            fileName = null;

            return false;
        }

        fileName = rulesetFileName;

        return true;
    }

    private static bool TryGetCodeAnalysisOverrideFileName(in BuildContext buildContext, [NotNullWhen(true)] out string? fileName)
    {
        string rulesetOverridesFileName = Path.Combine(buildContext.SourceDirectory,
                                                       buildContext.BuildOverride.PreRelease
                                                           ? "pre-release.rule-settings.json"
                                                           : "release.rule-settings.json");

        if (!File.Exists(rulesetOverridesFileName))
        {
            fileName = null;

            return false;
        }

        fileName = rulesetOverridesFileName;

        return true;
    }

    private bool ApplyChanges(XmlDocument ruleSet, IReadOnlyList<RuleChange> changes)
    {
        bool changed = false;

        foreach (RuleChange change in changes)
        {
            this._logger.ChangingState(change.RuleSet, rule: change.Rule, change.State);
            bool hasChanged = ruleSet.ChangeValue(ruleSet: change.RuleSet, rule: change.Rule, name: change.Description, newState: change.State, logger: this._logger);
            changed |= hasChanged;
        }

        return changed;
    }

    private async ValueTask<IAsyncDisposable?> SetCodeAnalysisConfigAsync(BuildContext buildContext, CancellationToken cancellationToken)
    {
        if (!TryGetCodeAnalysisFileName(buildContext, out string? rulesetFileName))
        {
            return null;
        }

        if (!TryGetCodeAnalysisOverrideFileName(buildContext, out string? rulesetOverridesFileName))
        {
            return null;
        }

        IReadOnlyList<RuleChange> changes = await ChangeSet.LoadAsync(rulesetOverridesFileName, cancellationToken);
        if (changes is not [])
        {
            XmlDocument ruleSet = await RuleSet.LoadAsync(rulesetFileName);
            if (this.ApplyChanges(ruleSet: ruleSet, changes: changes))
            {
                byte[] originalContents = await File.ReadAllBytesAsync(path: rulesetFileName, cancellationToken: cancellationToken);
                await RuleSet.SaveAsync(rulesetFileName, ruleSet);

                return new FileRestorer(rulesetFileName, originalContents);
            }
        }

        return null;
    }

    private sealed class FileRestorer : IAsyncDisposable
    {
        private readonly byte[] _content;
        private readonly string _fileName;

        public FileRestorer(string fileName, byte[] content)
        {
            this._fileName = fileName;
            this._content = content;
        }

        public async ValueTask DisposeAsync()
        {
            await File.WriteAllBytesAsync(this._fileName, this._content, CancellationToken.None);
        }
    }
}