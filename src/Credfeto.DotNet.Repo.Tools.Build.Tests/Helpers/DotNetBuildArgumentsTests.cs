using System;
using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.Build.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Build.Tests.Helpers;

public sealed class DotNetBuildArgumentsTests : TestBase
{
    private const string PROJECT_WITH_SPACES = "/work dir/My Project/My Project.csproj";
    private const string SOLUTION_DIRECTORY_WITH_SPACES = "/work dir/My Solution";

    [Fact]
    public static void BuildProjectKeepsProjectPathWithSpacesAsSingleArgument()
    {
        IReadOnlyList<string> arguments = DotNetBuildArguments.BuildProject(
            projectFileName: PROJECT_WITH_SPACES,
            solutionDirectory: SOLUTION_DIRECTORY_WITH_SPACES,
            preRelease: false
        );

        Assert.Equal(expected: "build", actual: arguments[0]);
        Assert.Equal(expected: PROJECT_WITH_SPACES, actual: arguments[1]);
    }

    [Fact]
    public static void BuildProjectKeepsSolutionDirWithSpacesAsSingleArgument()
    {
        IReadOnlyList<string> arguments = DotNetBuildArguments.BuildProject(
            projectFileName: PROJECT_WITH_SPACES,
            solutionDirectory: SOLUTION_DIRECTORY_WITH_SPACES,
            preRelease: false
        );

        Assert.Contains(
            expected: "-p:SolutionDir=" + SOLUTION_DIRECTORY_WITH_SPACES,
            collection: arguments,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public static void TestPassesFiltersWithoutEmbeddedQuotes()
    {
        IReadOnlyList<string> arguments = DotNetBuildArguments.Test();

        Assert.DoesNotContain(
            collection: arguments,
            filter: argument => argument.Contains(value: '"', comparisonType: StringComparison.Ordinal)
        );

        int index = IndexOf(arguments: arguments, value: "--filter-not-namespace");
        Assert.True(index >= 0, userMessage: "Missing --filter-not-namespace");
        Assert.Equal(expected: "*.BenchMark.Tests", actual: arguments[index + 1]);
    }

    [Theory]
    [InlineData(false, "--nowarn:NU1802")]
    [InlineData(true, "--nowarn:NU1802;NU1901;NU1902;NU1903;NU1904")]
    public static void BuildIncludesNoWarnForReleaseMode(bool preRelease, string expected)
    {
        IReadOnlyList<string> arguments = DotNetBuildArguments.Build(preRelease: preRelease);

        Assert.Equal(expected: expected, actual: arguments[^1]);
    }

    [Fact]
    public static void PublishWithoutFrameworkOmitsFrameworkArgument()
    {
        IReadOnlyList<string> arguments = DotNetBuildArguments.Publish(framework: null, preRelease: false);

        Assert.DoesNotContain(
            collection: arguments,
            filter: argument => argument.StartsWith(value: "--framework:", comparisonType: StringComparison.Ordinal)
        );
    }

    [Fact]
    public static void PublishWithFrameworkIncludesFrameworkArgument()
    {
        IReadOnlyList<string> arguments = DotNetBuildArguments.Publish(framework: "net10.0", preRelease: false);

        Assert.Contains(expected: "--framework:net10.0", collection: arguments, comparer: StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void NoCommandContainsEmptyArguments(bool preRelease)
    {
        IReadOnlyList<IReadOnlyList<string>> commands =
        [
            DotNetBuildArguments.BuildServerShutdown(),
            DotNetBuildArguments.Clean(preRelease: preRelease),
            DotNetBuildArguments.Restore(preRelease: preRelease),
            DotNetBuildArguments.Build(preRelease: preRelease),
            DotNetBuildArguments.BuildProject(
                projectFileName: PROJECT_WITH_SPACES,
                solutionDirectory: SOLUTION_DIRECTORY_WITH_SPACES,
                preRelease: preRelease
            ),
            DotNetBuildArguments.Test(),
            DotNetBuildArguments.Pack(preRelease: preRelease),
            DotNetBuildArguments.Publish(framework: null, preRelease: preRelease),
            DotNetBuildArguments.Publish(framework: "net10.0", preRelease: preRelease),
        ];

        foreach (IReadOnlyList<string> command in commands)
        {
            Assert.NotEmpty(command);
            Assert.DoesNotContain(collection: command, filter: string.IsNullOrWhiteSpace);
        }
    }

    private static int IndexOf(IReadOnlyList<string> arguments, string value)
    {
        for (int index = 0; index < arguments.Count; ++index)
        {
            if (StringComparer.Ordinal.Equals(x: arguments[index], y: value))
            {
                return index;
            }
        }

        return -1;
    }
}
