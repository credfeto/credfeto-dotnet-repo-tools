using System.Threading.Tasks;
using System.Xml;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

public sealed partial class ProjectXmlRewriterTests
{
    private async Task DoNormaliseReferenceMetadataAsync(string expectedXml, string originalXml)
    {
        string txtExpected = await GetValidatedExpectedDocumentAsync(expectedXml);

        XmlDocument doc = LoadXml(originalXml);

        this._projectXmlRewriter.NormaliseReferenceMetadata(projectDocument: doc, filename: "test.csproj");

        await this.DoComparaisonAsync(doc: doc, txtExpected: txtExpected);
    }

    [Fact]
    public Task ShouldConvertSingleVersionChildElementToAttributeAsync()
    {
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"">
      <Version>1.0.0</Version>
    </PackageReference>
  </ItemGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";

        return this.DoNormaliseReferenceMetadataAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ShouldConvertMultipleMetadataChildElementsToAttributesAsync()
    {
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"">
      <Version>1.0.0</Version>
      <PrivateAssets>All</PrivateAssets>
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"" Version=""1.0.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
  </ItemGroup>
</Project>";

        return this.DoNormaliseReferenceMetadataAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ShouldConvertProjectReferenceChildElementToAttributeAsync()
    {
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Foo\Foo.csproj"">
      <Private>false</Private>
    </ProjectReference>
  </ItemGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Foo\Foo.csproj"" Private=""false"" />
  </ItemGroup>
</Project>";

        return this.DoNormaliseReferenceMetadataAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ShouldConvertFrameworkReferenceAndDotNetCliToolReferenceChildElementsAsync()
    {
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <FrameworkReference Include=""Microsoft.AspNetCore.App"">
      <PrivateAssets>All</PrivateAssets>
    </FrameworkReference>
    <DotNetCliToolReference Include=""Foo.Tool"">
      <Version>1.0.0</Version>
    </DotNetCliToolReference>
  </ItemGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <FrameworkReference Include=""Microsoft.AspNetCore.App"" PrivateAssets=""All"" />
    <DotNetCliToolReference Include=""Foo.Tool"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";

        return this.DoNormaliseReferenceMetadataAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ShouldLeaveAlreadyAttributeFormUnchangedAsync()
    {
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";

        return this.DoNormaliseReferenceMetadataAsync(expectedXml: xml, originalXml: xml);
    }

    [Fact]
    public Task ShouldLeaveChildWithConflictingAttributeValueUnchangedAsync()
    {
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"" Version=""1.0.0"">
      <Version>2.0.0</Version>
    </PackageReference>
  </ItemGroup>
</Project>";

        return this.DoNormaliseReferenceMetadataAsync(expectedXml: xml, originalXml: xml);
    }

    [Fact]
    public Task ShouldLeaveChildWithConditionAttributeUnchangedAsync()
    {
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"">
      <Version Condition=""'$(Configuration)'=='Debug'"">1.0.0</Version>
    </PackageReference>
  </ItemGroup>
</Project>";

        return this.DoNormaliseReferenceMetadataAsync(expectedXml: xml, originalXml: xml);
    }

    [Fact]
    public Task ShouldLeaveChildWithNestedElementUnchangedAsync()
    {
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"">
      <Version><Nested>1.0.0</Nested></Version>
    </PackageReference>
  </ItemGroup>
</Project>";

        return this.DoNormaliseReferenceMetadataAsync(expectedXml: xml, originalXml: xml);
    }

    [Fact]
    public Task ShouldLeaveChildWithCommentUnchangedAsync()
    {
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"">
      <Version><!-- pinned -->1.0.0</Version>
    </PackageReference>
  </ItemGroup>
</Project>";

        return this.DoNormaliseReferenceMetadataAsync(expectedXml: xml, originalXml: xml);
    }

    [Fact]
    public Task ShouldConvertOnlyEligibleChildrenAndLeaveTheRestWhenMixedAsync()
    {
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"">
      <Version>1.0.0</Version>
      <IncludeAssets Condition=""'$(Configuration)'=='Debug'"">all</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"" Version=""1.0.0"">
      <IncludeAssets Condition=""'$(Configuration)'=='Debug'"">all</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>";

        return this.DoNormaliseReferenceMetadataAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public void NormaliseReferenceMetadataShouldReturnFalseWhenNoProjectElement()
    {
        const string xml =
            @"<Root><ItemGroup><PackageReference Include=""Foo""><Version>1.0</Version></PackageReference></ItemGroup></Root>";
        XmlDocument doc = LoadXml(xml);

        bool result = this._projectXmlRewriter.NormaliseReferenceMetadata(
            projectDocument: doc,
            filename: "test.csproj"
        );

        Assert.False(result, userMessage: "Should return false when no Project element found");
    }

    [Fact]
    public void NormaliseReferenceMetadataShouldReturnFalseWhenNoChildElementsToConvert()
    {
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";
        XmlDocument doc = LoadXml(xml);

        bool result = this._projectXmlRewriter.NormaliseReferenceMetadata(
            projectDocument: doc,
            filename: "test.csproj"
        );

        Assert.False(result, userMessage: "Should return false when nothing needs converting");
    }

    [Fact]
    public void NormaliseReferenceMetadataShouldReturnTrueWhenConversionHappens()
    {
        const string xml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Foo.Bar"">
      <Version>1.0.0</Version>
    </PackageReference>
  </ItemGroup>
</Project>";
        XmlDocument doc = LoadXml(xml);

        bool result = this._projectXmlRewriter.NormaliseReferenceMetadata(
            projectDocument: doc,
            filename: "test.csproj"
        );

        Assert.True(result, userMessage: "Should return true when a child element was converted to an attribute");
    }
}
