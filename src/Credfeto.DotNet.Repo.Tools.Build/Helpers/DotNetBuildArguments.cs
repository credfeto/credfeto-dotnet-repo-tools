using System;
using System.Collections.Generic;
using System.Linq;

namespace Credfeto.DotNet.Repo.Tools.Build.Helpers;

// Each entry is passed to dotnet as a single argument, so values containing spaces must not be split or quoted.
public static class DotNetBuildArguments
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

    private static readonly IReadOnlyList<string> StandardBuildParameters =
    [
        Property(name: "Version", value: BUILD_VERSION),
        Property(name: "SuppressNETCoreSdkPreviewMessage", value: "True"),
        Property(name: "Optimize", value: "True"),
        Property(name: "ContinuousIntegrationBuild", value: "True"),
    ];

    private static readonly IReadOnlyList<string> PublishParameters =
    [
        Property(name: "DisableSwagger", value: "False"),
        Property(name: "IncludeNativeLibrariesForSelfExtract", value: "false"),
        Property(name: "PublishReadyToRun", value: "False"),
        Property(name: "PublishReadyToRunShowWarnings", value: "True"),
        Property(name: "PublishSingleFile", value: "true"),
        Property(name: "PublishTrimmed", value: "False"),
        Property(name: "TreatWarningsAsErrors", value: "True"),
        Property(name: "Version", value: BUILD_VERSION),
    ];

    private static readonly IReadOnlyList<string> TestFilters =
    [
        "--filter-not-namespace",
        "*.BenchMark.Tests",
        "--filter-not-namespace",
        "*.BenchMark.Tests.*",
        "--filter-not-namespace",
        "*.Integration.Tests",
        "--filter-not-namespace",
        "*.Integration.Tests.*",
    ];

    public static IReadOnlyList<string> BuildServerShutdown()
    {
        return ["build-server", "shutdown"];
    }

    public static IReadOnlyList<string> Clean(bool preRelease)
    {
        return ["clean", "--configuration:Release", "-nodeReuse:False", .. NoWarn(preRelease)];
    }

    public static IReadOnlyList<string> Restore(bool preRelease)
    {
        return ["restore", "-nodeReuse:False", "-r:linux-x64", .. NoWarn(preRelease)];
    }

    public static IReadOnlyList<string> Build(bool preRelease)
    {
        return
        [
            "build",
            "--no-restore",
            "-warnAsError",
            "-nodeReuse:False",
            "--configuration:Release",
            .. StandardBuildParameters,
            .. NoWarn(preRelease),
        ];
    }

    public static IReadOnlyList<string> BuildProject(string projectFileName, string solutionDirectory, bool preRelease)
    {
        return
        [
            "build",
            projectFileName,
            "-warnAsError",
            "-nodeReuse:False",
            "--configuration:Release",
            Property(name: "Version", value: BUILD_VERSION),
            Property(name: "SolutionDir", value: solutionDirectory),
            Property(name: "SuppressNETCoreSdkPreviewMessage", value: "True"),
            Property(name: "Optimize", value: "True"),
            Property(name: "ContinuousIntegrationBuild", value: "True"),
            .. NoWarn(preRelease),
        ];
    }

    public static IReadOnlyList<string> Test()
    {
        return
        [
            "test",
            "--no-build",
            "--no-restore",
            "--configuration:Release",
            "--long-running",
            "10",
            .. TestFilters,
            "--parallel-algorithm",
            "aggressive",
            "--ignore-exit-code",
            "8",
            .. StandardBuildParameters,
        ];
    }

    public static IReadOnlyList<string> Pack(bool preRelease)
    {
        return
        [
            "pack",
            "-nodeReuse:False",
            "--configuration:Release",
            .. StandardBuildParameters,
            .. NoWarn(preRelease),
        ];
    }

    public static IReadOnlyList<string> Publish(string? framework, bool preRelease)
    {
        return
        [
            "publish",
            "-warnaserror",
            "--configuration:Release",
            "-r:linux-x64",
            .. Framework(framework),
            "--self-contained",
            .. PublishParameters,
            "-nodeReuse:False",
            .. NoWarn(preRelease),
        ];
    }

    private static IReadOnlyList<string> Framework(string? framework)
    {
        return string.IsNullOrWhiteSpace(framework) ? [] : ["--framework:" + framework];
    }

    private static string Property(string name, string value)
    {
        return "-p:" + name + "=" + value;
    }

    private static IReadOnlyList<string> NoWarn(bool preRelease)
    {
        IReadOnlyList<string> items = preRelease
            ? [.. NoWarnPreRelease.Concat(NoWarnAll).Distinct(StringComparer.Ordinal)]
            : NoWarnAll;

        return ["--nowarn:" + string.Join(separator: ';', items.Order(comparer: StringComparer.Ordinal))];
    }
}
