using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

[SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "Unit tests")]
public sealed partial class ProjectXmlRewriterTests
{
    [Fact]
    public void ReOrderPropertyGroupsShouldReturnFalseWhenNoProjectElement()
    {
        const string xml = @"<Root><PropertyGroup><Foo>bar</Foo></PropertyGroup></Root>";
        XmlDocument doc = LoadXml(xml);

        bool result = this._projectXmlRewriter.ReOrderPropertyGroups(projectDocument: doc, filename: "test.csproj");

        Assert.False(result, userMessage: "Should return false when no Project element found");
    }

    [Fact]
    public void ReOrderIncludesShouldReturnFalseWhenNoProjectElement()
    {
        const string xml = @"<Root><ItemGroup><PackageReference Include=""Foo"" Version=""1.0""/></ItemGroup></Root>";
        XmlDocument doc = LoadXml(xml);

        bool result = this._projectXmlRewriter.ReOrderIncludes(projectDocument: doc, filename: "test.csproj");

        Assert.False(result, userMessage: "Should return false when no Project element found");
    }

    [Fact]
    public void ReOrderIncludesShouldReturnFalseForDuplicatePackageReference()
    {
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo"" Version=""1.0""/>
    <PackageReference Include=""Foo"" Version=""2.0""/>
  </ItemGroup>
</Project>";

        XmlDocument doc = LoadXml(xml);

        bool result = this._projectXmlRewriter.ReOrderIncludes(projectDocument: doc, filename: "test.csproj");

        Assert.False(result, userMessage: "Should return false when duplicate PackageReference found");
    }

    [Fact]
    public void ReOrderIncludesShouldReturnFalseForDuplicatePackageReferenceWithPrivateAssets()
    {
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo"" Version=""1.0"" PrivateAssets=""All""/>
    <PackageReference Include=""Foo"" Version=""2.0"" PrivateAssets=""All""/>
  </ItemGroup>
</Project>";

        XmlDocument doc = LoadXml(xml);

        bool result = this._projectXmlRewriter.ReOrderIncludes(projectDocument: doc, filename: "test.csproj");

        Assert.False(result, userMessage: "Should return false when duplicate PrivateAssets PackageReference found");
    }

    [Fact]
    public void ReOrderIncludesShouldReturnFalseForDuplicateProjectReference()
    {
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Foo\Foo.csproj""/>
    <ProjectReference Include=""..\Foo\Foo.csproj""/>
  </ItemGroup>
</Project>";

        XmlDocument doc = LoadXml(xml);

        bool result = this._projectXmlRewriter.ReOrderIncludes(projectDocument: doc, filename: "test.csproj");

        Assert.False(result, userMessage: "Should return false when duplicate ProjectReference found");
    }

    [Fact]
    public void ReOrderIncludesShouldReturnFalseForUnknownItemType()
    {
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <SomeUnknownItemType Include=""Something""/>
  </ItemGroup>
</Project>";

        XmlDocument doc = LoadXml(xml);

        bool result = this._projectXmlRewriter.ReOrderIncludes(projectDocument: doc, filename: "test.csproj");

        Assert.False(result, userMessage: "Should return false when unknown item type found");
    }

    [Fact]
    public void MergePropertiesOfMultipleGroupsShouldThrowXmlExceptionForDuplicatePropertyAcrossGroups()
    {
        // Two combinable groups that each have <Nullable> — when merged, this causes a duplicate
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <PropertyGroup>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>";

        XmlDocument doc = LoadXml(xml);
        XmlElement? project = doc.SelectSingleNode("Project") as XmlElement;
        Assert.NotNull(project);

        IReadOnlyList<XmlElement> propertyGroups =
        [
            .. project
                .ChildNodes.OfType<XmlElement>()
                .Where(n => StringComparer.Ordinal.Equals(x: n.Name, y: "PropertyGroup")),
        ];

        ProjectXmlRewriter concreteRewriter = new(this.GetTypedLogger<ProjectXmlRewriter>());
        Assert.Throws<XmlException>(() =>
            concreteRewriter.MergePropertiesOfMultipleGroups(fileName: "test.csproj", propertyGroups: propertyGroups)
        );
    }

    [Fact]
    public Task ReOrderPropertyGroupsShouldNotMergeWhenDefineConstantsFoundWithConditionAttributeAsync()
    {
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup Condition=""'$(Configuration)'=='Debug'"">
    <DefineConstants>$(DefineConstants);DEBUG</DefineConstants>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";

        // DefineConstants group won't be merged, so result should stay identical
        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup Condition=""'$(Configuration)'=='Debug'"">
    <DefineConstants>$(DefineConstants);DEBUG</DefineConstants>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertyGroupsShouldNotMergeGroupWithDefineConstantsAndNoAttributesAsync()
    {
        // Group without attributes but with DefineConstants - IsCombinableGroup returns false
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <DefineConstants>$(DefineConstants);DEBUG</DefineConstants>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <DefineConstants>$(DefineConstants);DEBUG</DefineConstants>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>";

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertyGroupShouldNotSortWhenDefineConstantsFoundAsync()
    {
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup Test=""True"">
    <DefineConstants>$(DefineConstants);DEBUG</DefineConstants>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup Test=""True"">
    <DefineConstants>$(DefineConstants);DEBUG</DefineConstants>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertyGroupShouldNotSortWhenDuplicateChildNameFoundAsync()
    {
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup Test=""True"">
    <Nullable>enable</Nullable>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup Test=""True"">
    <Nullable>enable</Nullable>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>";

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderIncludesShouldNotChangeItemGroupWithAttributesAsync()
    {
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup Condition=""'$(Configuration)'=='Debug'"">
    <PackageReference Include=""Foo"" Version=""1.0""/>
  </ItemGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup Condition=""'$(Configuration)'=='Debug'"">
    <PackageReference Include=""Foo"" Version=""1.0""/>
  </ItemGroup>
</Project>";

        return this.DoReOrderItemGroupsAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderIncludesShouldNotChangeItemGroupWithCommentsAsync()
    {
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <!-- A comment -->
    <PackageReference Include=""Foo"" Version=""1.0""/>
  </ItemGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <!-- A comment -->
    <PackageReference Include=""Foo"" Version=""1.0""/>
  </ItemGroup>
</Project>";

        return this.DoReOrderItemGroupsAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertyGroupsShouldNotMergeGroupWithDuplicateChildNamesAndNoAttributesAsync()
    {
        // PropertyGroup without attributes but with duplicate child names — IsCombinableGroup should return false
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>";

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }
}
