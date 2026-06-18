using System.Xml;
using Credfeto.DotNet.Repo.Tools.Dependencies.Helpers;
using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Helpers;

public sealed class ProjectExtractorTests : TestBase
{
    private static XmlElement CreateProjectReferenceElement(string include)
    {
        XmlDocument doc = new();
        XmlElement element = doc.CreateElement("ProjectReference");

        if (!string.IsNullOrEmpty(include))
        {
            element.SetAttribute("Include", include);
        }

        return element;
    }

    [Fact]
    public void ExtractProjectReferenceShouldReturnNullWhenNoIncludeAttribute()
    {
        XmlElement element = CreateProjectReferenceElement(include: "");
        ProjectReference? result = ProjectExtractor.ExtractProjectReference(element);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractProjectReferenceShouldReturnProjectReferenceWhenValid()
    {
        XmlElement element = CreateProjectReferenceElement(include: @"..\Child\Child.csproj");
        ProjectReference? result = ProjectExtractor.ExtractProjectReference(element);
        Assert.NotNull(result);
        Assert.Equal(expected: @"..\Child\Child.csproj", actual: result.RelativeInclude);
    }

    [Fact]
    public void ExtractProjectReferenceWithFileNameShouldReturnNullWhenNoInclude()
    {
        XmlElement element = CreateProjectReferenceElement(include: "");
        FileProjectReference? result = ProjectExtractor.ExtractProjectReference(fileName: "/base/dir", node: element);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractProjectReferenceWithFileNameShouldReturnFileProjectReferenceWhenValid()
    {
        XmlElement element = CreateProjectReferenceElement(include: @"..\Child\Child.csproj");
        FileProjectReference? result = ProjectExtractor.ExtractProjectReference(fileName: "/base/dir", node: element);
        Assert.NotNull(result);
        Assert.Equal(expected: "/base/dir", actual: result.File);
        Assert.Equal(expected: @"..\Child\Child.csproj", actual: result.RelativeInclude);
    }
}
