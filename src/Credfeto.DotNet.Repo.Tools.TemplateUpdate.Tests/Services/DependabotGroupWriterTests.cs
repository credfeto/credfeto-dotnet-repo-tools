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
            new(Name: "vitest", Patterns: ["vitest", "@vitest/*"]),
            new(Name: "eslint", Patterns: ["eslint"]),
        ];

        DependabotGroupWriter.AddGroups(config: config, groups: groups);

        Assert.Equal(
            expected:
            [
                "existing",
                "    groups:",
                "      vitest:",
                "        patterns:",
                "          - \"vitest\"",
                "          - \"@vitest/*\"",
                "      eslint:",
                "        patterns:",
                "          - \"eslint\"",
            ],
            actual: config
        );
    }
}
