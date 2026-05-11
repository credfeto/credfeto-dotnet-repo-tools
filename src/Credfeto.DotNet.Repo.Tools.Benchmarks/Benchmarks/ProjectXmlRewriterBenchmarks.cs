using System.Xml;
using BenchmarkDotNet.Attributes;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Credfeto.DotNet.Repo.Tools.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public sealed class ProjectXmlRewriterBenchmarks
{
    private readonly ProjectXmlRewriter _rewriter = new(NullLogger<ProjectXmlRewriter>.Instance);

    // Typical csproj with multiple PropertyGroups and unordered PackageReferences
    private const string SampleCsproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
            <Nullable>enable</Nullable>
            <LangVersion>latest</LangVersion>
            <Authors>Mark Ridgwell</Authors>
          </PropertyGroup>
          <PropertyGroup>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <OutputType>Exe</OutputType>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Serilog" Version="4.0.0" />
            <PackageReference Include="Cocona" Version="2.2.0" />
          </ItemGroup>
          <ItemGroup>
            <PackageReference Include="Meziantou.Analyzer" Version="2.0.212" PrivateAssets="All" ExcludeAssets="runtime" />
            <PackageReference Include="AsyncFixer" Version="1.6.0" PrivateAssets="All" ExcludeAssets="runtime" />
          </ItemGroup>
        </Project>
        """;

    [Benchmark]
    public bool ReOrderPropertyGroups()
    {
        XmlDocument doc = new();
        doc.LoadXml(SampleCsproj);

        return this._rewriter.ReOrderPropertyGroups(projectDocument: doc, filename: "test.csproj");
    }

    [Benchmark]
    public bool ReOrderIncludes()
    {
        XmlDocument doc = new();
        doc.LoadXml(SampleCsproj);

        return this._rewriter.ReOrderIncludes(projectDocument: doc, filename: "test.csproj");
    }
}
