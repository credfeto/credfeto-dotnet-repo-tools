using System;
using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

public sealed class DependabotConfigWriterTests : TestBase
{
    private static readonly IReadOnlyList<string> Header = ["---", "version: 2", "updates:"];

    [Fact]
    public void StartConfigWritesHeader()
    {
        List<string> config = DependabotConfigWriter.StartConfig();

        Assert.Equal(expected: Header, actual: config);
    }

    [Fact]
    public void StartConfigReturnsIndependentListEachCall()
    {
        List<string> first = DependabotConfigWriter.StartConfig();
        first.Add("extra");

        List<string> second = DependabotConfigWriter.StartConfig();

        Assert.Equal(expected: Header, actual: second);
    }

    [Fact]
    public void ToDocumentJoinsLinesWithNewLineAndAddsTrailingNewLine()
    {
        string result = DependabotConfigWriter.ToDocument(["a", "", "b"]);

        Assert.Equal(
            expected: "a" + Environment.NewLine + Environment.NewLine + "b" + Environment.NewLine,
            actual: result,
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public void AddBaseConfigDirectlyAfterUpdatesHasNoBlankLine()
    {
        List<string> config = DependabotConfigWriter.StartConfig();

        DependabotConfigWriter.AddBaseConfig(
            config: config,
            ecoSystem: "docker",
            directory: "/",
            packageTypeLabel: "docker",
            reviewer: "someone"
        );

        Assert.Equal(
            expected:
            [
                .. Header,
                .. BaseBlock(ecoSystem: "docker", directory: "/", packageTypeLabel: "docker", reviewer: "someone"),
            ],
            actual: config
        );
    }

    [Fact]
    public void AddBaseConfigAfterAnotherSectionAddsBlankLineSeparator()
    {
        List<string> config = ["---", "version: 2", "updates:", "    allow:"];

        DependabotConfigWriter.AddBaseConfig(
            config: config,
            ecoSystem: "pip",
            directory: "/tools",
            packageTypeLabel: "python",
            reviewer: "credfeto"
        );

        Assert.Equal(
            expected:
            [
                .. Header,
                "    allow:",
                "",
                .. BaseBlock(ecoSystem: "pip", directory: "/tools", packageTypeLabel: "python", reviewer: "credfeto"),
            ],
            actual: config
        );
    }

    [Fact]
    public void AllowAllDependenciesAddsAllowBlock()
    {
        List<string> config = ["existing"];

        DependabotConfigWriter.AllowAllDependencies(config);

        Assert.Equal(expected: ["existing", .. AllowBlock()], actual: config);
    }

    [Theory]
    [InlineData("gitsubmodule", "submodule")]
    [InlineData("docker", "docker")]
    [InlineData("github-actions", "github-actions")]
    [InlineData("pip", "python")]
    public static void AddEcosystemSectionWritesBaseConfigAndAllowBlock(string ecoSystem, string packageTypeLabel)
    {
        List<string> config = DependabotConfigWriter.StartConfig();

        DependabotConfigWriter.AddEcosystemSection(
            config: config,
            ecoSystem: ecoSystem,
            directory: "/",
            packageTypeLabel: packageTypeLabel,
            reviewer: "credfeto"
        );

        Assert.Equal(
            expected:
            [
                .. Header,
                .. BaseBlock(
                    ecoSystem: ecoSystem,
                    directory: "/",
                    packageTypeLabel: packageTypeLabel,
                    reviewer: "credfeto"
                ),
                .. AllowBlock(),
            ],
            actual: config
        );
    }

    [Fact]
    public void MultipleEcosystemSectionsAreSeparatedByBlankLines()
    {
        List<string> config = DependabotConfigWriter.StartConfig();

        DependabotConfigWriter.AddEcosystemSection(
            config: config,
            ecoSystem: "gitsubmodule",
            directory: "/",
            packageTypeLabel: "submodule",
            reviewer: "credfeto"
        );
        DependabotConfigWriter.AddEcosystemSection(
            config: config,
            ecoSystem: "docker",
            directory: "/",
            packageTypeLabel: "docker",
            reviewer: "credfeto"
        );

        Assert.Equal(
            expected:
            [
                .. Header,
                .. BaseBlock(
                    ecoSystem: "gitsubmodule",
                    directory: "/",
                    packageTypeLabel: "submodule",
                    reviewer: "credfeto"
                ),
                .. AllowBlock(),
                "",
                .. BaseBlock(ecoSystem: "docker", directory: "/", packageTypeLabel: "docker", reviewer: "credfeto"),
                .. AllowBlock(),
            ],
            actual: config
        );
    }

    [Fact]
    public void AddNpmSectionWritesVersioningStrategyBeforeAllowAndThenGroups()
    {
        List<string> config = DependabotConfigWriter.StartConfig();
        IReadOnlyList<DependabotGroup> groups =
        [
            new(Name: "vitest", AppliesTo: "version-updates", Patterns: ["vitest", "@vitest/*"]),
        ];

        DependabotConfigWriter.AddNpmSection(
            config: config,
            directory: "/web",
            packageTypeLabel: "npm",
            reviewer: "credfeto",
            groups: groups
        );

        Assert.Equal(
            expected:
            [
                .. Header,
                .. BaseBlock(ecoSystem: "npm", directory: "/web", packageTypeLabel: "npm", reviewer: "credfeto"),
                "    versioning-strategy: increase-if-necessary",
                .. AllowBlock(),
                "    groups:",
                "      vitest:",
                "        applies-to: version-updates",
                "        patterns:",
                "          - \"vitest\"",
                "          - \"@vitest/*\"",
            ],
            actual: config
        );
    }

    [Fact]
    public void AddNpmSectionWithNoGroupsOmitsGroupsBlock()
    {
        List<string> config = DependabotConfigWriter.StartConfig();

        DependabotConfigWriter.AddNpmSection(
            config: config,
            directory: "/",
            packageTypeLabel: "npm",
            reviewer: "credfeto",
            groups: []
        );

        Assert.Equal(
            expected:
            [
                .. Header,
                .. BaseBlock(ecoSystem: "npm", directory: "/", packageTypeLabel: "npm", reviewer: "credfeto"),
                "    versioning-strategy: increase-if-necessary",
                .. AllowBlock(),
            ],
            actual: config
        );
    }

    [Fact]
    public void AddNuGetSectionWithNoPackagesOmitsIgnoreBlock()
    {
        List<string> config = DependabotConfigWriter.StartConfig();

        DependabotConfigWriter.AddNuGetSection(
            config: config,
            directory: "/",
            packageTypeLabel: "nuget",
            reviewer: "credfeto",
            packages: []
        );

        Assert.Equal(
            expected:
            [
                .. Header,
                .. BaseBlock(ecoSystem: "nuget", directory: "/", packageTypeLabel: "nuget", reviewer: "credfeto"),
                .. AllowBlock(),
            ],
            actual: config
        );
    }

    [Fact]
    public void AddNuGetSectionWithPackagesWritesIgnoreBlockAfterAllow()
    {
        List<string> config = DependabotConfigWriter.StartConfig();

        DependabotConfigWriter.AddNuGetSection(
            config: config,
            directory: "/",
            packageTypeLabel: "nuget",
            reviewer: "credfeto",
            packages: [ExactPackage("FunFair.Test.Common"), WildcardPackage("Meziantou")]
        );

        Assert.Equal(
            expected:
            [
                .. Header,
                .. BaseBlock(ecoSystem: "nuget", directory: "/", packageTypeLabel: "nuget", reviewer: "credfeto"),
                .. AllowBlock(),
                "    ignore:",
                "      - dependency-name: \"FunFair.Test.Common\"",
                "      - dependency-name: \"Meziantou.*\"",
            ],
            actual: config
        );
    }

    [Fact]
    public void AddIgnoredPackagesWithNoPackagesAddsNothing()
    {
        List<string> config = ["existing"];

        DependabotConfigWriter.AddIgnoredPackages(config: config, packages: []);

        Assert.Equal(expected: ["existing"], actual: config);
    }

    [Fact]
    public void AddIgnoredPackagesWritesWildcardWithSuffixAndExactWithout()
    {
        List<string> config = [];

        DependabotConfigWriter.AddIgnoredPackages(
            config: config,
            packages: [WildcardPackage("Meziantou"), ExactPackage("Polly")]
        );

        Assert.Equal(
            expected: ["    ignore:", "      - dependency-name: \"Meziantou.*\"", "      - dependency-name: \"Polly\""],
            actual: config
        );
    }

    [Fact]
    public void AddIgnoredPackagesOrdersCaseInsensitively()
    {
        List<string> config = [];

        DependabotConfigWriter.AddIgnoredPackages(
            config: config,
            packages: [ExactPackage("zeta"), ExactPackage("Beta"), ExactPackage("alpha")]
        );

        Assert.Equal(
            expected:
            [
                "    ignore:",
                "      - dependency-name: \"alpha\"",
                "      - dependency-name: \"Beta\"",
                "      - dependency-name: \"zeta\"",
            ],
            actual: config
        );
    }

    [Fact]
    public void AddIgnoredPackagesWildcardShadowsExactPackageUnderIt()
    {
        List<string> config = [];

        DependabotConfigWriter.AddIgnoredPackages(
            config: config,
            packages: [ExactPackage("funfair.test.common"), WildcardPackage("FunFair")]
        );

        Assert.Equal(expected: ["    ignore:", "      - dependency-name: \"FunFair.*\""], actual: config);
    }

    [Fact]
    public void AddIgnoredPackagesWildcardShadowsExactPackageWithSameId()
    {
        List<string> config = [];

        DependabotConfigWriter.AddIgnoredPackages(
            config: config,
            packages: [ExactPackage("FUNFAIR"), WildcardPackage("FunFair")]
        );

        Assert.Equal(expected: ["    ignore:", "      - dependency-name: \"FunFair.*\""], actual: config);
    }

    [Fact]
    public void AddIgnoredPackagesWildcardShadowsLongerWildcardUnderIt()
    {
        List<string> config = [];

        DependabotConfigWriter.AddIgnoredPackages(
            config: config,
            packages: [WildcardPackage("FunFair.Test"), WildcardPackage("funfair")]
        );

        Assert.Equal(expected: ["    ignore:", "      - dependency-name: \"funfair.*\""], actual: config);
    }

    [Fact]
    public void AddIgnoredPackagesWildcardDoesNotShadowPackageSharingPrefixWithoutDot()
    {
        List<string> config = [];

        DependabotConfigWriter.AddIgnoredPackages(
            config: config,
            packages: [WildcardPackage("FunFair"), ExactPackage("FunFairExtra"), WildcardPackage("FunFairOther")]
        );

        Assert.Equal(
            expected:
            [
                "    ignore:",
                "      - dependency-name: \"FunFair.*\"",
                "      - dependency-name: \"FunFairExtra\"",
                "      - dependency-name: \"FunFairOther.*\"",
            ],
            actual: config
        );
    }

    [Fact]
    public void AddIgnoredPackagesDeduplicatesExactPackagesCaseInsensitivelyKeepingFirst()
    {
        List<string> config = [];

        DependabotConfigWriter.AddIgnoredPackages(
            config: config,
            packages: [ExactPackage("Polly.Core"), ExactPackage("polly.core"), ExactPackage("POLLY.CORE")]
        );

        Assert.Equal(expected: ["    ignore:", "      - dependency-name: \"Polly.Core\""], actual: config);
    }

    [Fact]
    public void AddGroupsWithNoGroupsAddsNothing()
    {
        List<string> config = ["existing"];

        DependabotConfigWriter.AddGroups(config: config, groups: []);

        Assert.Equal(expected: ["existing"], actual: config);
    }

    [Fact]
    public void AddGroupsWithMultipleGroupsWritesEachGroupInOrder()
    {
        List<string> config = ["existing"];
        IReadOnlyList<DependabotGroup> groups =
        [
            new(Name: "vitest", AppliesTo: "version-updates", Patterns: ["vitest", "@vitest/*"]),
            new(Name: "eslint-security", AppliesTo: "security-updates", Patterns: ["eslint"]),
        ];

        DependabotConfigWriter.AddGroups(config: config, groups: groups);

        Assert.Equal(
            expected:
            [
                "existing",
                "    groups:",
                "      vitest:",
                "        applies-to: version-updates",
                "        patterns:",
                "          - \"vitest\"",
                "          - \"@vitest/*\"",
                "      eslint-security:",
                "        applies-to: security-updates",
                "        patterns:",
                "          - \"eslint\"",
            ],
            actual: config
        );
    }

    private static IReadOnlyList<string> BaseBlock(
        string ecoSystem,
        string directory,
        string packageTypeLabel,
        string reviewer
    )
    {
        return
        [
            $"  - package-ecosystem: {ecoSystem}",
            $"    directory: \"{directory}\"",
            "    schedule:",
            "      interval: daily",
            "      time: \"03:00\"",
            "      timezone: \"Europe/London\"",
            "    open-pull-requests-limit: 99",
            "    assignees:",
            $"      - {reviewer}",
            "    commit-message:",
            "      prefix: \"[Dependencies]\"",
            "    labels:",
            $"      - \"{packageTypeLabel}\"",
            "      - \"dependencies\"",
            "      - \"Changelog Not Required\"",
        ];
    }

    private static IReadOnlyList<string> AllowBlock()
    {
        return ["    allow:", "      - dependency-type: all"];
    }

    private static PackageUpdate WildcardPackage(string packageId)
    {
        return new PackageUpdate(
            packageId: packageId,
            packageType: "all",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );
    }

    private static PackageUpdate ExactPackage(string packageId)
    {
        return new PackageUpdate(
            packageId: packageId,
            packageType: "all",
            exactMatch: true,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );
    }
}
