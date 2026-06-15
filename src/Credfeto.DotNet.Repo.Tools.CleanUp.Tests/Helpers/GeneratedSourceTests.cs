using System.IO;
using Credfeto.DotNet.Repo.Tools.CleanUp.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Helpers;

public sealed class GeneratedSourceTests : TestBase
{
    [Fact]
    public void IsGeneratedShouldReturnTrueForPathContainingObjDirectory()
    {
        string path = Path.Combine("C:", "project", "obj", "generated", "file.cs");
        Assert.True(
            GeneratedSource.IsGenerated(path),
            userMessage: "Expected path containing /obj/ to be detected as generated"
        );
    }

    [Fact]
    public void IsGeneratedShouldReturnTrueForPathContainingGeneratedDirectory()
    {
        string path = Path.Combine("C:", "project", "generated", "file.cs");
        Assert.True(
            GeneratedSource.IsGenerated(path),
            userMessage: "Expected path containing /generated/ to be detected as generated"
        );
    }

    [Fact]
    public void IsGeneratedShouldReturnTrueForPathContainingDotGeneratedDot()
    {
        const string path = "C:/project/src/file.generated.cs";
        Assert.True(
            GeneratedSource.IsGenerated(path),
            userMessage: "Expected path containing .generated. to be detected as generated"
        );
    }

    [Fact]
    public void IsGeneratedShouldReturnFalseForNormalPath()
    {
        string path = Path.Combine("C:", "project", "src", "file.cs");
        Assert.False(
            GeneratedSource.IsGenerated(path),
            userMessage: "Expected normal source path to not be detected as generated"
        );
    }

    [Fact]
    public void IsGeneratedShouldReturnFalseForSimpleFilename()
    {
        const string path = "Program.cs";
        Assert.False(
            GeneratedSource.IsGenerated(path),
            userMessage: "Expected simple filename to not be detected as generated"
        );
    }

    [Fact]
    public void IsNonGeneratedShouldReturnFalseForObjPath()
    {
        string path = Path.Combine("C:", "project", "obj", "file.cs");
        Assert.False(GeneratedSource.IsNonGenerated(path), userMessage: "Expected obj path to not be non-generated");
    }

    [Fact]
    public void IsNonGeneratedShouldReturnTrueForNormalPath()
    {
        string path = Path.Combine("C:", "project", "src", "file.cs");
        Assert.True(
            GeneratedSource.IsNonGenerated(path),
            userMessage: "Expected normal source path to be non-generated"
        );
    }

    [Fact]
    public void IsNonGeneratedShouldReturnFalseForGeneratedDirectory()
    {
        string path = Path.Combine("C:", "project", "generated", "file.cs");
        Assert.False(
            GeneratedSource.IsNonGenerated(path),
            userMessage: "Expected generated path to not be non-generated"
        );
    }

    [Fact]
    public void IsNonGeneratedShouldReturnFalseForDotGeneratedDot()
    {
        const string path = "C:/project/src/file.generated.cs";
        Assert.False(
            GeneratedSource.IsNonGenerated(path),
            userMessage: "Expected .generated. path to not be non-generated"
        );
    }
}
