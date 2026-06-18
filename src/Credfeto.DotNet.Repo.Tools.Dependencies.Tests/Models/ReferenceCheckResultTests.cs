using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Models;

public sealed class ReferenceCheckResultTests : TestBase
{
    [Fact]
    public void FullConstructorShouldSetAllProperties()
    {
        ReferenceCheckResult sut = new(
            ProjectFileName: "test.csproj",
            Type: ReferenceType.PACKAGE,
            Name: "SomePackage",
            Version: "1.0.0"
        );
        Assert.Equal(expected: "test.csproj", actual: sut.ProjectFileName);
        Assert.Equal(expected: ReferenceType.PACKAGE, actual: sut.Type);
        Assert.Equal(expected: "SomePackage", actual: sut.Name);
        Assert.Equal(expected: "1.0.0", actual: sut.Version);
    }

    [Fact]
    public void ShortConstructorShouldSetVersionToNull()
    {
        ReferenceCheckResult sut = new(
            ProjectFileName: "test.csproj",
            Type: ReferenceType.PROJECT,
            Name: "SomeProject"
        );
        Assert.Equal(expected: "test.csproj", actual: sut.ProjectFileName);
        Assert.Equal(expected: ReferenceType.PROJECT, actual: sut.Type);
        Assert.Equal(expected: "SomeProject", actual: sut.Name);
        Assert.Null(sut.Version);
    }

    [Fact]
    public void SdkTypeShouldBeSupported()
    {
        ReferenceCheckResult sut = new(
            ProjectFileName: "test.csproj",
            Type: ReferenceType.SDK,
            Name: "Microsoft.NET.Sdk.Web"
        );
        Assert.Equal(expected: ReferenceType.SDK, actual: sut.Type);
    }
}
