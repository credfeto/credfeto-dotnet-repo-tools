using Credfeto.DotNet.Repo.Tools.Models.Packages;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Models.Tests.Packages;

public sealed class PackageExcludeTests : TestBase
{
    [Fact]
    public void ConstructorSetsPropertiesWhenExactMatchTrue()
    {
        const string packageId = "Package.Id";

        PackageExclude exclude = new(packageId: packageId, exactMatch: true);

        Assert.Equal(expected: packageId, actual: exclude.PackageId);
        Assert.True(condition: exclude.ExactMatch, userMessage: "ExactMatch should be true");
    }

    [Fact]
    public void ConstructorSetsPropertiesWhenExactMatchFalse()
    {
        const string packageId = "Another.Package.Id";

        PackageExclude exclude = new(packageId: packageId, exactMatch: false);

        Assert.Equal(expected: packageId, actual: exclude.PackageId);
        Assert.False(condition: exclude.ExactMatch, userMessage: "ExactMatch should be false");
    }
}
