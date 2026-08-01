using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Models.Tests.Packages;

public sealed class PackageUpdateTests : TestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructorSetsProperties(bool hasExclude)
    {
        IReadOnlyList<PackageExclude>? exclude = hasExclude
            ? [new(packageId: "Excluded.Package", exactMatch: false)]
            : null;

        PackageUpdate update = new(
            packageId: "Package.Id",
            packageType: "Nuget",
            exactMatch: true,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: true,
            exclude: exclude
        );

        Assert.Equal(expected: "Package.Id", actual: update.PackageId);
        Assert.Equal(expected: "Nuget", actual: update.PackageType);
        Assert.True(condition: update.ExactMatch, userMessage: "ExactMatch should be true");
        Assert.False(condition: update.VersionBumpPackage, userMessage: "VersionBumpPackage should be false");
        Assert.True(
            condition: update.ProhibitVersionBumpWhenReferenced,
            userMessage: "ProhibitVersionBumpWhenReferenced should be true"
        );
        Assert.Equal(expected: exclude, actual: update.Exclude);
    }
}
