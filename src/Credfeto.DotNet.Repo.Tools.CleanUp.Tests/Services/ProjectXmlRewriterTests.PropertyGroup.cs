using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

[SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "Unit tests")]
public sealed partial class ProjectXmlRewriterTests
{
    [Fact]
    public Task ReOrderPropertieShouldNotChangeAnythingWhenCommentsDetectedAsync()
    {
        const string originalXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
    <EnablePackageValidation>true</EnablePackageValidation>
    <EnableTrimAnalyzer>false</EnableTrimAnalyzer>
    <Features>strict;flow-analysis</Features>
    <GenerateNeutralResourcesLanguageAttribute>true</GenerateNeutralResourcesLanguageAttribute>
    <ImplicitUsings>disable</ImplicitUsings>
    <IncludeOpenAPIAnalyzers>true</IncludeOpenAPIAnalyzers>
    <IsPackable>true</IsPackable>
    <IsPublishable>false</IsPublishable>
    <IsTrimmable>false</IsTrimmable>
    <LangVersion>latest</LangVersion>
    <NoWarn />
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <RunAOTCompilation>false</RunAOTCompilation>
    <TargetFrameworks>net6.0;net7.0;net8.0</TargetFrameworks>
    <TieredCompilation>true</TieredCompilation>
    <TreatSpecificWarningsAsErrors />
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>true</EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
    <OptimizationPreference>speed</OptimizationPreference>
    <!-- Dotnet 7 features -->
    <TieredPGO>true</TieredPGO>
    <UseSystemResourceKeys>true</UseSystemResourceKeys>
    <IlcOptimizationPreference>Size</IlcOptimizationPreference>
    <IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>
    <DebuggerSupport>true</DebuggerSupport>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <NuGetAudit>true</NuGetAudit>
  </PropertyGroup>
  <PropertyGroup>
    <!-- Code Anlalysis -->
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    <CodeAnalysisRuleSet>$(SolutionDir)\CodeAnalysis.ruleset</CodeAnalysisRuleSet>
    <CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnforceExtendedAnalyzerRules>false</EnforceExtendedAnalyzerRules>
  </PropertyGroup>
  <PropertyGroup>
    <!-- Package properties -->
    <Authors>Mark Ridgwell</Authors>
    <Company>Mark Ridgwell</Company>
    <Copyright>Mark Ridgwell</Copyright>
    <Description>Dotnet Repository Update tools</Description>
    <NeutralLanguage>en-GB</NeutralLanguage>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReleaseNotes>$(ReleaseNotes)</PackageReleaseNotes>
    <PackageTags>Nuget;update;tool;packages</PackageTags>
    <Product>Repository Update Tool</Product>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>https://github.com/credfeto/credfeto-dotnet-repo-tools</RepositoryUrl>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces.csproj"" />
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Packages.Interfaces\Credfeto.DotNet.Repo.Tools.Packages.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Release.Interfaces\Credfeto.DotNet.Repo.Tools.Release.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tracking.Interfaces\Credfeto.DotNet.Repo.Tracking.Interfaces.csproj""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""Credfeto.ChangeLog"" Version=""1.10.19.111""/>
    <PackageReference Include=""Microsoft.Extensions.DependencyInjection.Abstractions"" Version=""8.0.1"" />
    <PackageReference Include=""Microsoft.Extensions.Logging.Abstractions"" Version=""8.0.1""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""AsyncFixer"" Version=""1.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""codecracker.CSharp"" Version=""1.1.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Credfeto.Enumeration.Source.Generation"" Version=""1.1.6.354"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""CSharpIsNullAnalyzer"" Version=""0.1.495"" PrivateAssets=""all"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""FunFair.CodeAnalysis"" Version=""7.0.14.369"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Meziantou.Analyzer"" Version=""2.0.149"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Microsoft.VisualStudio.Threading.Analyzers"" Version=""17.9.28"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Nullable.Extended.Analyzer"" Version=""1.15.6169"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.DuplicateCodeAnalyzer"" Version=""1.1.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.MaintainabilityAnalyzers"" Version=""1.5.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Roslynator.Analyzers"" Version=""4.12.1"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SecurityCodeScan.VS2019"" Version=""5.6.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SmartAnalyzers.CSharpExtensions.Annotations"" Version=""4.2.11"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SonarAnalyzer.CSharp"" Version=""9.24.0.89429"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SourceLink.Create.CommandLine"" Version=""2.8.3"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""ToStringWithoutOverrideAnalyzer"" Version=""0.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
  </ItemGroup>
</Project>";

        const string expectedXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
    <EnablePackageValidation>true</EnablePackageValidation>
    <EnableTrimAnalyzer>false</EnableTrimAnalyzer>
    <Features>strict;flow-analysis</Features>
    <GenerateNeutralResourcesLanguageAttribute>true</GenerateNeutralResourcesLanguageAttribute>
    <ImplicitUsings>disable</ImplicitUsings>
    <IncludeOpenAPIAnalyzers>true</IncludeOpenAPIAnalyzers>
    <IsPackable>true</IsPackable>
    <IsPublishable>false</IsPublishable>
    <IsTrimmable>false</IsTrimmable>
    <LangVersion>latest</LangVersion>
    <NoWarn />
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <RunAOTCompilation>false</RunAOTCompilation>
    <TargetFrameworks>net6.0;net7.0;net8.0</TargetFrameworks>
    <TieredCompilation>true</TieredCompilation>
    <TreatSpecificWarningsAsErrors />
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>true</EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
    <OptimizationPreference>speed</OptimizationPreference>
    <!-- Dotnet 7 features -->
    <TieredPGO>true</TieredPGO>
    <UseSystemResourceKeys>true</UseSystemResourceKeys>
    <IlcOptimizationPreference>Size</IlcOptimizationPreference>
    <IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>
    <DebuggerSupport>true</DebuggerSupport>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <NuGetAudit>true</NuGetAudit>
  </PropertyGroup>
  <PropertyGroup>
    <!-- Code Anlalysis -->
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    <CodeAnalysisRuleSet>$(SolutionDir)\CodeAnalysis.ruleset</CodeAnalysisRuleSet>
    <CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnforceExtendedAnalyzerRules>false</EnforceExtendedAnalyzerRules>
  </PropertyGroup>
  <PropertyGroup>
    <!-- Package properties -->
    <Authors>Mark Ridgwell</Authors>
    <Company>Mark Ridgwell</Company>
    <Copyright>Mark Ridgwell</Copyright>
    <Description>Dotnet Repository Update tools</Description>
    <NeutralLanguage>en-GB</NeutralLanguage>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReleaseNotes>$(ReleaseNotes)</PackageReleaseNotes>
    <PackageTags>Nuget;update;tool;packages</PackageTags>
    <Product>Repository Update Tool</Product>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>https://github.com/credfeto/credfeto-dotnet-repo-tools</RepositoryUrl>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces.csproj"" />
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Packages.Interfaces\Credfeto.DotNet.Repo.Tools.Packages.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Release.Interfaces\Credfeto.DotNet.Repo.Tools.Release.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tracking.Interfaces\Credfeto.DotNet.Repo.Tracking.Interfaces.csproj""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""Credfeto.ChangeLog"" Version=""1.10.19.111""/>
    <PackageReference Include=""Microsoft.Extensions.DependencyInjection.Abstractions"" Version=""8.0.1"" />
    <PackageReference Include=""Microsoft.Extensions.Logging.Abstractions"" Version=""8.0.1""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""AsyncFixer"" Version=""1.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""codecracker.CSharp"" Version=""1.1.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Credfeto.Enumeration.Source.Generation"" Version=""1.1.6.354"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""CSharpIsNullAnalyzer"" Version=""0.1.495"" PrivateAssets=""all"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""FunFair.CodeAnalysis"" Version=""7.0.14.369"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Meziantou.Analyzer"" Version=""2.0.149"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Microsoft.VisualStudio.Threading.Analyzers"" Version=""17.9.28"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Nullable.Extended.Analyzer"" Version=""1.15.6169"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.DuplicateCodeAnalyzer"" Version=""1.1.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.MaintainabilityAnalyzers"" Version=""1.5.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Roslynator.Analyzers"" Version=""4.12.1"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SecurityCodeScan.VS2019"" Version=""5.6.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SmartAnalyzers.CSharpExtensions.Annotations"" Version=""4.2.11"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SonarAnalyzer.CSharp"" Version=""9.24.0.89429"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SourceLink.Create.CommandLine"" Version=""2.8.3"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""ToStringWithoutOverrideAnalyzer"" Version=""0.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
  </ItemGroup>
</Project>";

        return this.DoReOorderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertieShouldSortButNotCombineWhenAttributesDetectedAsync()
    {
        const string originalXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup Test=""True"">
    <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
    <EnablePackageValidation>true</EnablePackageValidation>
    <EnableTrimAnalyzer>false</EnableTrimAnalyzer>
    <Features>strict;flow-analysis</Features>
    <GenerateNeutralResourcesLanguageAttribute>true</GenerateNeutralResourcesLanguageAttribute>
    <ImplicitUsings>disable</ImplicitUsings>
    <IncludeOpenAPIAnalyzers>true</IncludeOpenAPIAnalyzers>
    <IsPackable>true</IsPackable>
    <IsPublishable>false</IsPublishable>
    <IsTrimmable>false</IsTrimmable>
    <LangVersion>latest</LangVersion>
    <NoWarn />
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <RunAOTCompilation>false</RunAOTCompilation>
    <TargetFrameworks>net6.0;net7.0;net8.0</TargetFrameworks>
    <TieredCompilation>true</TieredCompilation>
    <TreatSpecificWarningsAsErrors />
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>true</EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
    <OptimizationPreference>speed</OptimizationPreference>
    <TieredPGO>true</TieredPGO>
    <UseSystemResourceKeys>true</UseSystemResourceKeys>
    <IlcOptimizationPreference>Size</IlcOptimizationPreference>
    <IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>
    <DebuggerSupport>true</DebuggerSupport>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <NuGetAudit>true</NuGetAudit>
  </PropertyGroup>
  <PropertyGroup Test=""True"">
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    <CodeAnalysisRuleSet>$(SolutionDir)\CodeAnalysis.ruleset</CodeAnalysisRuleSet>
    <CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnforceExtendedAnalyzerRules>false</EnforceExtendedAnalyzerRules>
  </PropertyGroup>
  <PropertyGroup Test=""True"">
    <Authors>Mark Ridgwell</Authors>
    <Company>Mark Ridgwell</Company>
    <Copyright>Mark Ridgwell</Copyright>
    <Description>Dotnet Repository Update tools</Description>
    <NeutralLanguage>en-GB</NeutralLanguage>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReleaseNotes>$(ReleaseNotes)</PackageReleaseNotes>
    <PackageTags>Nuget;update;tool;packages</PackageTags>
    <Product>Repository Update Tool</Product>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>https://github.com/credfeto/credfeto-dotnet-repo-tools</RepositoryUrl>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces.csproj"" />
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Packages.Interfaces\Credfeto.DotNet.Repo.Tools.Packages.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Release.Interfaces\Credfeto.DotNet.Repo.Tools.Release.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tracking.Interfaces\Credfeto.DotNet.Repo.Tracking.Interfaces.csproj""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""Credfeto.ChangeLog"" Version=""1.10.19.111""/>
    <PackageReference Include=""Microsoft.Extensions.DependencyInjection.Abstractions"" Version=""8.0.1"" />
    <PackageReference Include=""Microsoft.Extensions.Logging.Abstractions"" Version=""8.0.1""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""AsyncFixer"" Version=""1.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""codecracker.CSharp"" Version=""1.1.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Credfeto.Enumeration.Source.Generation"" Version=""1.1.6.354"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""CSharpIsNullAnalyzer"" Version=""0.1.495"" PrivateAssets=""all"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""FunFair.CodeAnalysis"" Version=""7.0.14.369"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Meziantou.Analyzer"" Version=""2.0.149"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Microsoft.VisualStudio.Threading.Analyzers"" Version=""17.9.28"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Nullable.Extended.Analyzer"" Version=""1.15.6169"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.DuplicateCodeAnalyzer"" Version=""1.1.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.MaintainabilityAnalyzers"" Version=""1.5.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Roslynator.Analyzers"" Version=""4.12.1"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SecurityCodeScan.VS2019"" Version=""5.6.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SmartAnalyzers.CSharpExtensions.Annotations"" Version=""4.2.11"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SonarAnalyzer.CSharp"" Version=""9.24.0.89429"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SourceLink.Create.CommandLine"" Version=""2.8.3"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""ToStringWithoutOverrideAnalyzer"" Version=""0.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
  </ItemGroup>
</Project>";

        const string expectedXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup Test=""True"">
    <DebuggerSupport>true</DebuggerSupport>
    <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
    <EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>true</EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>
    <EnablePackageValidation>true</EnablePackageValidation>
    <EnableTrimAnalyzer>false</EnableTrimAnalyzer>
    <Features>strict;flow-analysis</Features>
    <GenerateNeutralResourcesLanguageAttribute>true</GenerateNeutralResourcesLanguageAttribute>
    <IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>
    <IlcOptimizationPreference>Size</IlcOptimizationPreference>
    <ImplicitUsings>disable</ImplicitUsings>
    <IncludeOpenAPIAnalyzers>true</IncludeOpenAPIAnalyzers>
    <IncludeSymbols>true</IncludeSymbols>
    <IsPackable>true</IsPackable>
    <IsPublishable>false</IsPublishable>
    <IsTrimmable>false</IsTrimmable>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
    <LangVersion>latest</LangVersion>
    <NoWarn />
    <NuGetAudit>true</NuGetAudit>
    <Nullable>enable</Nullable>
    <OptimizationPreference>speed</OptimizationPreference>
    <OutputType>Library</OutputType>
    <RunAOTCompilation>false</RunAOTCompilation>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <TargetFrameworks>net6.0;net7.0;net8.0</TargetFrameworks>
    <TieredCompilation>true</TieredCompilation>
    <TieredPGO>true</TieredPGO>
    <TreatSpecificWarningsAsErrors />
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <UseSystemResourceKeys>true</UseSystemResourceKeys>
    <WarningsAsErrors />
  </PropertyGroup>
  <PropertyGroup Test=""True"">
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    <CodeAnalysisRuleSet>$(SolutionDir)\CodeAnalysis.ruleset</CodeAnalysisRuleSet>
    <CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnforceExtendedAnalyzerRules>false</EnforceExtendedAnalyzerRules>
  </PropertyGroup>
  <PropertyGroup Test=""True"">
    <Authors>Mark Ridgwell</Authors>
    <Company>Mark Ridgwell</Company>
    <Copyright>Mark Ridgwell</Copyright>
    <Description>Dotnet Repository Update tools</Description>
    <NeutralLanguage>en-GB</NeutralLanguage>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReleaseNotes>$(ReleaseNotes)</PackageReleaseNotes>
    <PackageTags>Nuget;update;tool;packages</PackageTags>
    <Product>Repository Update Tool</Product>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>https://github.com/credfeto/credfeto-dotnet-repo-tools</RepositoryUrl>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces.csproj"" />
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Packages.Interfaces\Credfeto.DotNet.Repo.Tools.Packages.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Release.Interfaces\Credfeto.DotNet.Repo.Tools.Release.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tracking.Interfaces\Credfeto.DotNet.Repo.Tracking.Interfaces.csproj""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""Credfeto.ChangeLog"" Version=""1.10.19.111""/>
    <PackageReference Include=""Microsoft.Extensions.DependencyInjection.Abstractions"" Version=""8.0.1"" />
    <PackageReference Include=""Microsoft.Extensions.Logging.Abstractions"" Version=""8.0.1""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""AsyncFixer"" Version=""1.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""codecracker.CSharp"" Version=""1.1.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Credfeto.Enumeration.Source.Generation"" Version=""1.1.6.354"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""CSharpIsNullAnalyzer"" Version=""0.1.495"" PrivateAssets=""all"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""FunFair.CodeAnalysis"" Version=""7.0.14.369"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Meziantou.Analyzer"" Version=""2.0.149"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Microsoft.VisualStudio.Threading.Analyzers"" Version=""17.9.28"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Nullable.Extended.Analyzer"" Version=""1.15.6169"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.DuplicateCodeAnalyzer"" Version=""1.1.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.MaintainabilityAnalyzers"" Version=""1.5.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Roslynator.Analyzers"" Version=""4.12.1"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SecurityCodeScan.VS2019"" Version=""5.6.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SmartAnalyzers.CSharpExtensions.Annotations"" Version=""4.2.11"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SonarAnalyzer.CSharp"" Version=""9.24.0.89429"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SourceLink.Create.CommandLine"" Version=""2.8.3"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""ToStringWithoutOverrideAnalyzer"" Version=""0.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
  </ItemGroup>
</Project>";

        return this.DoReOorderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }

    [Fact]
    public Task ReOrderPropertieShouldChangeToAlphanumericWhenNoCommentsDetectedAsync()
    {
        const string originalXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
    <EnablePackageValidation>true</EnablePackageValidation>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <Features>strict;flow-analysis</Features>
    <GenerateNeutralResourcesLanguageAttribute>true</GenerateNeutralResourcesLanguageAttribute>
    <ImplicitUsings>disable</ImplicitUsings>
    <IncludeOpenAPIAnalyzers>true</IncludeOpenAPIAnalyzers>
    <IsPackable>true</IsPackable>
    <IsPublishable>false</IsPublishable>
    <IsTrimmable>false</IsTrimmable>
    <LangVersion>latest</LangVersion>
    <NoWarn />
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <RunAOTCompilation>false</RunAOTCompilation>
    <TargetFrameworks>net6.0;net7.0;net8.0</TargetFrameworks>
    <TieredCompilation>true</TieredCompilation>
    <TreatSpecificWarningsAsErrors />
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>true</EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
    <OptimizationPreference>speed</OptimizationPreference>
    <TieredPGO>true</TieredPGO>
    <UseSystemResourceKeys>true</UseSystemResourceKeys>
    <IlcOptimizationPreference>Size</IlcOptimizationPreference>
    <IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>
    <DebuggerSupport>true</DebuggerSupport>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <NuGetAudit>true</NuGetAudit>
  </PropertyGroup>
  <PropertyGroup>
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    <CodeAnalysisRuleSet>$(SolutionDir)\CodeAnalysis.ruleset</CodeAnalysisRuleSet>
    <CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnforceExtendedAnalyzerRules>false</EnforceExtendedAnalyzerRules>
  </PropertyGroup>
  <PropertyGroup>
    <Authors>Mark Ridgwell</Authors>
    <Company>Mark Ridgwell</Company>
    <Copyright>Mark Ridgwell</Copyright>
    <Description>Dotnet Repository Update tools</Description>
    <NeutralLanguage>en-GB</NeutralLanguage>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReleaseNotes>$(ReleaseNotes)</PackageReleaseNotes>
    <PackageTags>Nuget;update;tool;packages</PackageTags>
    <Product>Repository Update Tool</Product>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>https://github.com/credfeto/credfeto-dotnet-repo-tools</RepositoryUrl>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces.csproj"" />
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Packages.Interfaces\Credfeto.DotNet.Repo.Tools.Packages.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Release.Interfaces\Credfeto.DotNet.Repo.Tools.Release.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tracking.Interfaces\Credfeto.DotNet.Repo.Tracking.Interfaces.csproj""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""Credfeto.ChangeLog"" Version=""1.10.19.111""/>
    <PackageReference Include=""Microsoft.Extensions.DependencyInjection.Abstractions"" Version=""8.0.1"" />
    <PackageReference Include=""Microsoft.Extensions.Logging.Abstractions"" Version=""8.0.1""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""AsyncFixer"" Version=""1.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""codecracker.CSharp"" Version=""1.1.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Credfeto.Enumeration.Source.Generation"" Version=""1.1.6.354"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""CSharpIsNullAnalyzer"" Version=""0.1.495"" PrivateAssets=""all"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""FunFair.CodeAnalysis"" Version=""7.0.14.369"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Meziantou.Analyzer"" Version=""2.0.149"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Microsoft.VisualStudio.Threading.Analyzers"" Version=""17.9.28"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Nullable.Extended.Analyzer"" Version=""1.15.6169"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.DuplicateCodeAnalyzer"" Version=""1.1.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.MaintainabilityAnalyzers"" Version=""1.5.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Roslynator.Analyzers"" Version=""4.12.1"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SecurityCodeScan.VS2019"" Version=""5.6.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SmartAnalyzers.CSharpExtensions.Annotations"" Version=""4.2.11"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SonarAnalyzer.CSharp"" Version=""9.24.0.89429"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SourceLink.Create.CommandLine"" Version=""2.8.3"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""ToStringWithoutOverrideAnalyzer"" Version=""0.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
  </ItemGroup>
</Project>";

        const string expectedXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    <Authors>Mark Ridgwell</Authors>
    <CodeAnalysisRuleSet>$(SolutionDir)\CodeAnalysis.ruleset</CodeAnalysisRuleSet>
    <CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>
    <Company>Mark Ridgwell</Company>
    <Copyright>Mark Ridgwell</Copyright>
    <DebuggerSupport>true</DebuggerSupport>
    <Description>Dotnet Repository Update tools</Description>
    <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
    <EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>true</EnableMicrosoftExtensionsConfigurationBinderSourceGenerator>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <EnablePackageValidation>true</EnablePackageValidation>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnforceExtendedAnalyzerRules>false</EnforceExtendedAnalyzerRules>
    <Features>strict;flow-analysis</Features>
    <GenerateNeutralResourcesLanguageAttribute>true</GenerateNeutralResourcesLanguageAttribute>
    <IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>
    <IlcOptimizationPreference>Size</IlcOptimizationPreference>
    <ImplicitUsings>disable</ImplicitUsings>
    <IncludeOpenAPIAnalyzers>true</IncludeOpenAPIAnalyzers>
    <IncludeSymbols>true</IncludeSymbols>
    <IsPackable>true</IsPackable>
    <IsPublishable>false</IsPublishable>
    <IsTrimmable>false</IsTrimmable>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
    <LangVersion>latest</LangVersion>
    <NeutralLanguage>en-GB</NeutralLanguage>
    <NoWarn />
    <NuGetAudit>true</NuGetAudit>
    <Nullable>enable</Nullable>
    <OptimizationPreference>speed</OptimizationPreference>
    <OutputType>Library</OutputType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReleaseNotes>$(ReleaseNotes)</PackageReleaseNotes>
    <PackageTags>Nuget;update;tool;packages</PackageTags>
    <Product>Repository Update Tool</Product>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>https://github.com/credfeto/credfeto-dotnet-repo-tools</RepositoryUrl>
    <RunAOTCompilation>false</RunAOTCompilation>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <TargetFrameworks>net6.0;net7.0;net8.0</TargetFrameworks>
    <TieredCompilation>true</TieredCompilation>
    <TieredPGO>true</TieredPGO>
    <TreatSpecificWarningsAsErrors />
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <UseSystemResourceKeys>true</UseSystemResourceKeys>
    <WarningsAsErrors />
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces\Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces.csproj"" />
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Packages.Interfaces\Credfeto.DotNet.Repo.Tools.Packages.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tools.Release.Interfaces\Credfeto.DotNet.Repo.Tools.Release.Interfaces.csproj""/>
    <ProjectReference Include=""..\Credfeto.DotNet.Repo.Tracking.Interfaces\Credfeto.DotNet.Repo.Tracking.Interfaces.csproj""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""Credfeto.ChangeLog"" Version=""1.10.19.111""/>
    <PackageReference Include=""Microsoft.Extensions.DependencyInjection.Abstractions"" Version=""8.0.1"" />
    <PackageReference Include=""Microsoft.Extensions.Logging.Abstractions"" Version=""8.0.1""/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""AsyncFixer"" Version=""1.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""codecracker.CSharp"" Version=""1.1.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Credfeto.Enumeration.Source.Generation"" Version=""1.1.6.354"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""CSharpIsNullAnalyzer"" Version=""0.1.495"" PrivateAssets=""all"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""FunFair.CodeAnalysis"" Version=""7.0.14.369"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Meziantou.Analyzer"" Version=""2.0.149"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Microsoft.VisualStudio.Threading.Analyzers"" Version=""17.9.28"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Nullable.Extended.Analyzer"" Version=""1.15.6169"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.DuplicateCodeAnalyzer"" Version=""1.1.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Philips.CodeAnalysis.MaintainabilityAnalyzers"" Version=""1.5.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""Roslynator.Analyzers"" Version=""4.12.1"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SecurityCodeScan.VS2019"" Version=""5.6.7"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SmartAnalyzers.CSharpExtensions.Annotations"" Version=""4.2.11"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SonarAnalyzer.CSharp"" Version=""9.24.0.89429"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""SourceLink.Create.CommandLine"" Version=""2.8.3"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
    <PackageReference Include=""ToStringWithoutOverrideAnalyzer"" Version=""0.6.0"" PrivateAssets=""All"" ExcludeAssets=""runtime"" />
  </ItemGroup>
</Project>";

        return this.DoReOorderPropertiesAsync(expectedXml: expectedXml, originalXml: originalXml);
    }
}