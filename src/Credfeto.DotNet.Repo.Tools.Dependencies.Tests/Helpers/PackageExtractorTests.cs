using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.DotNet.Repo.Tools.Dependencies.Helpers;
using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Helpers;

public sealed class PackageExtractorTests : TestBase
{
    private static readonly ValueTask NoOpRemoval = ValueTask.CompletedTask;

    private static XmlElement CreatePackageReferenceElement(
        string include,
        string? version = null,
        string? privateAssets = null,
        bool privateAssetsAsChild = false
    )
    {
        XmlDocument doc = new();
        XmlElement element = doc.CreateElement("PackageReference");

        if (!string.IsNullOrEmpty(include))
        {
            element.SetAttribute("Include", include);
        }

        if (version is not null)
        {
            element.SetAttribute("Version", version);
        }

        if (privateAssets is not null)
        {
            if (privateAssetsAsChild)
            {
                XmlElement child = doc.CreateElement("PrivateAssets");
                child.InnerText = privateAssets;
                element.AppendChild(child);
            }
            else
            {
                element.SetAttribute("PrivateAssets", privateAssets);
            }
        }

        return element;
    }

    [Fact]
    public void ExtractPackageReferenceShouldReturnNullWhenNoIncludeAttribute()
    {
        XmlElement element = CreatePackageReferenceElement(include: "", version: "1.0.0");
        PackageReference? result = PackageExtractor.ExtractPackageReference(element);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractPackageReferenceShouldReturnNullWhenNoVersionAttribute()
    {
        XmlElement element = CreatePackageReferenceElement(include: "SomePackage");
        PackageReference? result = PackageExtractor.ExtractPackageReference(element);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractPackageReferenceShouldReturnNullWhenPrivateAssetsAttributePresent()
    {
        XmlElement element = CreatePackageReferenceElement(
            include: "SomePackage",
            version: "1.0.0",
            privateAssets: "All"
        );
        PackageReference? result = PackageExtractor.ExtractPackageReference(element);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractPackageReferenceShouldReturnNullWhenPrivateAssetsChildNodePresent()
    {
        XmlElement element = CreatePackageReferenceElement(
            include: "SomePackage",
            version: "1.0.0",
            privateAssets: "All",
            privateAssetsAsChild: true
        );
        PackageReference? result = PackageExtractor.ExtractPackageReference(element);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractPackageReferenceShouldReturnPackageReferenceWhenValid()
    {
        XmlElement element = CreatePackageReferenceElement(include: "SomePackage", version: "1.0.0");
        PackageReference? result = PackageExtractor.ExtractPackageReference(element);
        Assert.NotNull(result);
        Assert.Equal(expected: "SomePackage", actual: result.PackageId);
        Assert.Equal(expected: "1.0.0", actual: result.Version);
    }

    [Fact]
    public void ExtractPackageReferenceWithConfigShouldReturnNullForDoNotRemovePackage()
    {
        ReferenceConfig config = new(onSuccessfulRemoval: static (_, _, _) => NoOpRemoval);
        XmlElement element = CreatePackageReferenceElement(include: "xunit", version: "2.9.0");
        List<string> allPackageIds = ["xunit"];

        FilePackageReference? result = PackageExtractor.ExtractPackageReference(
            config: config,
            node: element,
            allPackageIds: allPackageIds,
            baseDir: "/some/dir"
        );

        Assert.Null(result);
    }

    [Fact]
    public void ExtractPackageReferenceWithConfigShouldReturnNullWhenNoInclude()
    {
        ReferenceConfig config = new(onSuccessfulRemoval: static (_, _, _) => NoOpRemoval);
        XmlElement element = CreatePackageReferenceElement(include: "");
        List<string> allPackageIds = [];

        FilePackageReference? result = PackageExtractor.ExtractPackageReference(
            config: config,
            node: element,
            allPackageIds: allPackageIds,
            baseDir: "/some/dir"
        );

        Assert.Null(result);
    }

    [Fact]
    public void ExtractPackageReferenceWithConfigShouldReturnFilePackageReferenceWhenValid()
    {
        ReferenceConfig config = new(onSuccessfulRemoval: static (_, _, _) => NoOpRemoval);
        XmlElement element = CreatePackageReferenceElement(include: "SomePackage", version: "2.0.0");
        List<string> allPackageIds = ["SomePackage"];

        FilePackageReference? result = PackageExtractor.ExtractPackageReference(
            config: config,
            node: element,
            allPackageIds: allPackageIds,
            baseDir: "/base/dir"
        );

        Assert.NotNull(result);
        Assert.Equal(expected: "/base/dir", actual: result.File);
        Assert.Equal(expected: "SomePackage", actual: result.PackageId);
        Assert.Equal(expected: "2.0.0", actual: result.Version);
    }
}
