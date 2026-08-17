using System.Threading.Tasks;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

public sealed partial class ProjectXmlRewriterTests
{
    [Fact]
    public Task ReOrderPropertiesShouldNotReorderWhenPropertyReferencesAnotherInTheSameGroupAsync()
    {
        // PackageVersion references Version via $(Version). Alphabetical order would place
        // PackageVersion before Version, which would break the reference, so both must be left as-is.
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Version>1.2.3</Version>
    <PackageVersion>$(Version)-beta</PackageVersion>
  </PropertyGroup>
</Project>";

        const string expectedXml = originalXml;

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertiesShouldNotMergeWhenPropertyReferencesAnotherAcrossCombinableGroupsAsync()
    {
        // Version and PackageVersion are in separate combinable groups within the same run, so merging
        // them would place PackageVersion (which references $(Version)) before Version, breaking the
        // reference. Both groups must be left as-is, unmerged.
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Version>1.2.3</Version>
  </PropertyGroup>
  <PropertyGroup>
    <PackageVersion>$(Version)-beta</PackageVersion>
  </PropertyGroup>
</Project>";

        const string expectedXml = originalXml;

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertiesShouldNotThrowForDuplicatePropertyWhenGroupsAreSeparatedByImportAsync()
    {
        // Two combinable groups both define <Nullable>, but an Import between them means they belong to
        // separate runs and must never be merged, so this must not throw a duplicate-property exception.
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <Import Project=""Some.props"" />
  <PropertyGroup>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>";

        const string expectedXml = originalXml;

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertiesShouldSortEachRunIndependentlyWhenSeparatedByConditionalGroupAsync()
    {
        // A conditional PropertyGroup between two combinable groups blocks them from being merged into a
        // single group, but each side is still sorted independently within its own run.
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Bravo>1</Bravo>
    <Alpha>2</Alpha>
  </PropertyGroup>
  <PropertyGroup Condition=""'$(Configuration)'=='Debug'"">
    <DebugOnly>true</DebugOnly>
  </PropertyGroup>
  <PropertyGroup>
    <Delta>3</Delta>
    <Charlie>4</Charlie>
  </PropertyGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Alpha>2</Alpha>
    <Bravo>1</Bravo>
  </PropertyGroup>
  <PropertyGroup Condition=""'$(Configuration)'=='Debug'"">
    <DebugOnly>true</DebugOnly>
  </PropertyGroup>
  <PropertyGroup>
    <Charlie>4</Charlie>
    <Delta>3</Delta>
  </PropertyGroup>
</Project>";

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertiesShouldNotReorderWhenConditionAttributeReferencesAnotherPropertyInTheSameGroupAsync()
    {
        // RuntimeIdentifier's Condition attribute references $(TargetFramework). Alphabetical order would
        // place RuntimeIdentifier before TargetFramework, which would break the reference, so both must be
        // left as-is.
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifier Condition=""'$(TargetFramework)'=='net8.0'"">win-x64</RuntimeIdentifier>
  </PropertyGroup>
</Project>";

        const string expectedXml = originalXml;

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertiesShouldMergeCombinableGroupsSeparatedByAnItemGroupAsync()
    {
        // ItemGroups do not participate in MSBuild's property evaluation order, so they must not block
        // merging of combinable PropertyGroups either side of them.
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Bravo>1</Bravo>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Foo"" Version=""1.0""/>
  </ItemGroup>
  <PropertyGroup>
    <Alpha>2</Alpha>
  </PropertyGroup>
</Project>";

        const string expectedXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Alpha>2</Alpha>
    <Bravo>1</Bravo>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Foo"" Version=""1.0""/>
  </ItemGroup>
</Project>";

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertiesShouldNotMergeCombinableGroupsSeparatedByAChooseAsync()
    {
        // A Choose/When/Otherwise block can define properties conditionally in document order,
        // the same evaluation-order hazard as an Import, so it must also block merging of
        // combinable PropertyGroups either side of it.
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Bravo>1</Bravo>
  </PropertyGroup>
  <Choose>
    <When Condition=""'$(Configuration)'=='Debug'"">
      <PropertyGroup>
        <DebugOnly>true</DebugOnly>
      </PropertyGroup>
    </When>
  </Choose>
  <PropertyGroup>
    <Alpha>2</Alpha>
  </PropertyGroup>
</Project>";

        const string expectedXml = originalXml;

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertiesShouldNotReorderWhenReferenceCasingDiffersFromDefinitionAsync()
    {
        // MSBuild property names are case-insensitive, so $(version) still refers to <Version>.
        // Alphabetical order would place PackageVersion before Version, which would break the
        // reference despite the casing mismatch, so both must be left as-is.
        const string originalXml =
            @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Version>1.2.3</Version>
    <PackageVersion>$(version)-beta</PackageVersion>
  </PropertyGroup>
</Project>";

        const string expectedXml = originalXml;

        return this.DoReOrderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }
}
