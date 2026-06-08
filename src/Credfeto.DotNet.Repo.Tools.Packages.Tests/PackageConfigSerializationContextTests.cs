using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Packages.Tests;

public sealed class PackageConfigSerializationContextTests : TestBase
{
    [Fact]
    public void ShouldProvideTypeInfoForPackageUpdate()
    {
        Assert.NotNull(PackageConfigSerializationContext.Default.PackageUpdate);
    }

    [Fact]
    public void ShouldProvideTypeInfoForPackageExclude()
    {
        Assert.NotNull(PackageConfigSerializationContext.Default.PackageExclude);
    }

    [Fact]
    public void ShouldProvideTypeInfoForIReadOnlyListPackageUpdate()
    {
        Assert.NotNull(PackageConfigSerializationContext.Default.IReadOnlyListPackageUpdate);
    }

    [Fact]
    public void ShouldProvideTypeInfoForIReadOnlyListPackageExclude()
    {
        Assert.NotNull(PackageConfigSerializationContext.Default.IReadOnlyListPackageExclude);
    }

    [Fact]
    public void ShouldProvideTypeInfoForBoolean()
    {
        Assert.NotNull(PackageConfigSerializationContext.Default.Boolean);
    }

    [Fact]
    public void ShouldProvideTypeInfoForString()
    {
        Assert.NotNull(PackageConfigSerializationContext.Default.String);
    }

    [Fact]
    public void ShouldResolveTypeInfoForPackageUpdateByType()
    {
        JsonTypeInfo? typeInfo = PackageConfigSerializationContext.Default.GetTypeInfo(typeof(PackageUpdate));

        Assert.NotNull(typeInfo);
    }

    [Fact]
    public void ShouldReturnNullForTypeNotInContext()
    {
        JsonTypeInfo? typeInfo = PackageConfigSerializationContext.Default.GetTypeInfo(typeof(int));

        Assert.Null(typeInfo);
    }

    [Fact]
    public void ShouldSerializePackageUpdateWithNoExcludes()
    {
        PackageUpdate package = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );

        string json = JsonSerializer.Serialize(
            value: package,
            jsonTypeInfo: PackageConfigSerializationContext.Default.PackageUpdate
        );

        Assert.NotEmpty(json);
        Assert.Contains("Test.Package", json, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldSerializePackageExclude()
    {
        PackageExclude exclude = new(packageId: "Excluded.Package", exactMatch: true);

        string json = JsonSerializer.Serialize(
            value: exclude,
            jsonTypeInfo: PackageConfigSerializationContext.Default.PackageExclude
        );

        Assert.NotEmpty(json);
        Assert.Contains("Excluded.Package", json, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldSerializeIReadOnlyListOfPackageUpdates()
    {
        IReadOnlyList<PackageUpdate> packages =
        [
            new PackageUpdate(
                packageId: "Test.Package",
                packageType: "nuget",
                exactMatch: false,
                versionBumpPackage: false,
                prohibitVersionBumpWhenReferenced: false,
                exclude: null
            ),
        ];

        string json = JsonSerializer.Serialize(
            value: packages,
            jsonTypeInfo: PackageConfigSerializationContext.Default.IReadOnlyListPackageUpdate
        );

        Assert.NotEmpty(json);
    }

    [Fact]
    public void ShouldSerializeIReadOnlyListOfPackageExcludes()
    {
        IReadOnlyList<PackageExclude> excludes = [new PackageExclude(packageId: "Excluded.Package", exactMatch: false)];

        string json = JsonSerializer.Serialize(
            value: excludes,
            jsonTypeInfo: PackageConfigSerializationContext.Default.IReadOnlyListPackageExclude
        );

        Assert.NotEmpty(json);
    }

    [Fact]
    public void ShouldDeserializePackageWithExcludeList()
    {
        const string json = """
            [
              {
                "packageId": "Test.Package",
                "type": "nuget",
                "exact-match": false,
                "version-bump-package": false,
                "prohibit-version-bump-when-referenced": false,
                "exclude": [
                  {"packageId": "Excluded.Package", "exact-match": true}
                ]
              }
            ]
            """;

        IReadOnlyList<PackageUpdate>? result = JsonSerializer.Deserialize(
            json: json,
            jsonTypeInfo: PackageConfigSerializationContext.Default.IReadOnlyListPackageUpdate
        );

        Assert.NotNull(result);
        Assert.Single(result);
        IReadOnlyList<PackageExclude>? excludes = result[0].Exclude;
        Assert.NotNull(excludes);
        Assert.Single(excludes);
    }

    [Fact]
    public void ShouldSerializePackageUpdateWithExcludes()
    {
        PackageUpdate package = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: [new PackageExclude(packageId: "Excluded.Package", exactMatch: true)]
        );

        string json = JsonSerializer.Serialize(
            value: package,
            jsonTypeInfo: PackageConfigSerializationContext.Default.PackageUpdate
        );

        Assert.NotEmpty(json);
        Assert.Contains("Excluded.Package", json, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldCreateContextWithNoArgConstructor()
    {
        PackageConfigSerializationContext ctx = new();

        Assert.NotNull(ctx);
    }

    [Fact]
    public void ShouldDeserializePackageUpdateUsingNewContextInstance()
    {
        const string json = """
            [
              {
                "packageId": "Test.Package",
                "type": "nuget",
                "exact-match": false,
                "version-bump-package": false,
                "prohibit-version-bump-when-referenced": false,
                "exclude": [
                  {"packageId": "Excluded.Package", "exact-match": false}
                ]
              }
            ]
            """;

        PackageConfigSerializationContext ctx = new();
        IReadOnlyList<PackageUpdate>? result = JsonSerializer.Deserialize(
            json: json,
            jsonTypeInfo: ctx.IReadOnlyListPackageUpdate
        );

        Assert.NotNull(result);
        Assert.Single(result);
    }
}
