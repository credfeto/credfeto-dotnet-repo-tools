using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Packages.Services;
using Credfeto.Package;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Packages.Tests.Services;

public sealed class PackageUpdateConfigurationBuilderTests : LoggingFolderCleanupTestBase
{
    private readonly IPackageUpdateConfigurationBuilder _builder;

    public PackageUpdateConfigurationBuilderTests(ITestOutputHelper output)
        : base(output)
    {
        this._builder = new PackageUpdateConfigurationBuilder(
            logger: this.GetTypedLogger<PackageUpdateConfigurationBuilder>()
        );
    }

    [Fact]
    public void BuildWithNoExcludesReturnsCorrectPackageMatch()
    {
        PackageUpdate package = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );

        PackageUpdateConfiguration result = this._builder.Build(package);

        Assert.Equal(expected: "Test.Package", actual: result.PackageMatch.PackageId);
    }

    [Fact]
    public void BuildWithNoExcludesUsesPrefix()
    {
        PackageUpdate package = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );

        PackageUpdateConfiguration result = this._builder.Build(package);

        Assert.True(result.PackageMatch.Prefix, userMessage: "Prefix should be true when ExactMatch is false");
    }

    [Fact]
    public void BuildWithExactMatchDoesNotUsePrefix()
    {
        PackageUpdate package = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: true,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );

        PackageUpdateConfiguration result = this._builder.Build(package);

        Assert.False(result.PackageMatch.Prefix, userMessage: "Prefix should be false when ExactMatch is true");
    }

    [Fact]
    public void BuildWithNoExcludesReturnsEmptyExcludedPackages()
    {
        PackageUpdate package = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );

        PackageUpdateConfiguration result = this._builder.Build(package);

        Assert.Empty(result.ExcludedPackages);
    }

    [Fact]
    public void BuildWithEmptyExcludesReturnsEmptyExcludedPackages()
    {
        PackageUpdate package = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: []
        );

        PackageUpdateConfiguration result = this._builder.Build(package);

        Assert.Empty(result.ExcludedPackages);
    }

    [Fact]
    public void BuildWithExcludesReturnsCorrectExcludedPackageCount()
    {
        PackageExclude exclude = new(packageId: "Excluded.Package", exactMatch: false);
        PackageUpdate package = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: [exclude]
        );

        PackageUpdateConfiguration result = this._builder.Build(package);

        Assert.Single(result.ExcludedPackages);
    }

    [Fact]
    public void BuildWithExcludesReturnsCorrectExcludedPackageId()
    {
        PackageExclude exclude = new(packageId: "Excluded.Package", exactMatch: false);
        PackageUpdate package = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: [exclude]
        );

        PackageUpdateConfiguration result = this._builder.Build(package);

        Assert.Equal(expected: "Excluded.Package", actual: result.ExcludedPackages[0].PackageId);
    }

    [Fact]
    public void BuildWithExactMatchExcludeDoesNotUsePrefix()
    {
        PackageExclude exclude = new(packageId: "Excluded.Package", exactMatch: true);
        PackageUpdate package = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: [exclude]
        );

        PackageUpdateConfiguration result = this._builder.Build(package);

        Assert.False(result.ExcludedPackages[0].Prefix, userMessage: "Prefix should be false when ExactMatch is true");
    }

    [Fact]
    public void BuildWithMultipleExcludesReturnsAllExcludedPackages()
    {
        IReadOnlyList<PackageExclude> excludes =
        [
            new PackageExclude(packageId: "Excluded.A", exactMatch: false),
            new PackageExclude(packageId: "Excluded.B", exactMatch: true),
        ];

        PackageUpdate package = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: excludes
        );

        PackageUpdateConfiguration result = this._builder.Build(package);

        Assert.Equal(expected: 2, actual: result.ExcludedPackages.Count);
    }
}
