using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Models.Tests.Packages;

public sealed class PackageUpdateTests : TestBase
{
    [Fact]
    public void ConstructorSetsPropertiesWhenExcludeIsNull()
    {
        PackageUpdate update = new(
            packageId: "Package.Id",
            packageType: "Nuget",
            exactMatch: true,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: true,
            exclude: null
        );

        Assert.Equal(expected: "Package.Id", actual: update.PackageId);
        Assert.Equal(expected: "Nuget", actual: update.PackageType);
        Assert.True(condition: update.ExactMatch, userMessage: "ExactMatch should be true");
        Assert.False(condition: update.VersionBumpPackage, userMessage: "VersionBumpPackage should be false");
        Assert.True(
            condition: update.ProhibitVersionBumpWhenReferenced,
            userMessage: "ProhibitVersionBumpWhenReferenced should be true"
        );
        Assert.Null(update.Exclude);
    }

    [Fact]
    public void ConstructorSetsPropertiesWhenExcludeIsPopulated()
    {
        IReadOnlyList<PackageExclude> exclude = [new(packageId: "Excluded.Package", exactMatch: false)];

        PackageUpdate update = new(
            packageId: "Package.Id",
            packageType: "Npm",
            exactMatch: false,
            versionBumpPackage: true,
            prohibitVersionBumpWhenReferenced: false,
            exclude: exclude
        );

        Assert.Equal(expected: "Package.Id", actual: update.PackageId);
        Assert.Equal(expected: "Npm", actual: update.PackageType);
        Assert.False(condition: update.ExactMatch, userMessage: "ExactMatch should be false");
        Assert.True(condition: update.VersionBumpPackage, userMessage: "VersionBumpPackage should be true");
        Assert.False(
            condition: update.ProhibitVersionBumpWhenReferenced,
            userMessage: "ProhibitVersionBumpWhenReferenced should be false"
        );
        Assert.Equal(expected: exclude, actual: update.Exclude);
    }
}
