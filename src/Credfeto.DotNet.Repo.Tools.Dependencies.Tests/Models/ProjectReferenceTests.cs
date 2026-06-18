using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Models;

public sealed class ProjectReferenceTests : TestBase
{
    [Fact]
    public void ProjectReferenceShouldHaveCorrectProperties()
    {
        ProjectReference sut = new(@"..\Child\Child.csproj");
        Assert.Equal(expected: @"..\Child\Child.csproj", actual: sut.RelativeInclude);
    }

    [Fact]
    public void ToFileProjectReferenceShouldReturnCorrectValues()
    {
        ProjectReference sut = new(@"..\Child\Child.csproj");
        FileProjectReference result = sut.ToFileProjectReference("/base/dir");
        Assert.Equal(expected: "/base/dir", actual: result.File);
        Assert.Equal(expected: @"..\Child\Child.csproj", actual: result.RelativeInclude);
    }
}
