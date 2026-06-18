using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Models;

public sealed class FileProjectReferenceTests : TestBase
{
    [Fact]
    public void FileProjectReferenceShouldHaveCorrectProperties()
    {
        FileProjectReference sut = new(File: "/some/path", RelativeInclude: @"..\Other\Other.csproj");
        Assert.Equal(expected: "/some/path", actual: sut.File);
        Assert.Equal(expected: @"..\Other\Other.csproj", actual: sut.RelativeInclude);
    }
}
