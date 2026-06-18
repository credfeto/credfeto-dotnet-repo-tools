using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Models;

public sealed class PackageReferenceTests : TestBase
{
    [Fact]
    public void PackageReferenceShouldHaveCorrectProperties()
    {
        PackageReference sut = new(PackageId: "SomePackage", Version: "1.2.3");
        Assert.Equal(expected: "SomePackage", actual: sut.PackageId);
        Assert.Equal(expected: "1.2.3", actual: sut.Version);
    }

    [Fact]
    public void ToFilePackageReferenceShouldReturnCorrectValues()
    {
        PackageReference sut = new(PackageId: "SomePackage", Version: "1.2.3");
        FilePackageReference result = sut.ToFilePackageReference("/base/dir");
        Assert.Equal(expected: "/base/dir", actual: result.File);
        Assert.Equal(expected: "SomePackage", actual: result.PackageId);
        Assert.Equal(expected: "1.2.3", actual: result.Version);
    }
}
