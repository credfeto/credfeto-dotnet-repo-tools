using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

public sealed class DependabotGroupWriterTests : TestBase
{
    [Fact]
    public void AddGroupsWithNoGroupsAddsNothing()
    {
        List<string> config = ["existing"];

        DependabotGroupWriter.AddGroups(config: config, groups: []);

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

        DependabotGroupWriter.AddGroups(config: config, groups: groups);

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
}
