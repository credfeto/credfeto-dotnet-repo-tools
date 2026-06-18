using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Models;

public sealed class FilePackageReferenceTests : TestBase
{
    [Fact]
    public void FilePackageReferenceShouldHaveCorrectProperties()
    {
        FilePackageReference sut = new(File: "/some/path", PackageId: "MyPackage", Version: "3.0.0");
        Assert.Equal(expected: "/some/path", actual: sut.File);
        Assert.Equal(expected: "MyPackage", actual: sut.PackageId);
        Assert.Equal(expected: "3.0.0", actual: sut.Version);
    }
}
