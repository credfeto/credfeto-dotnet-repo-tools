using Credfeto.DotNet.Repo.Tools.Models.Packages;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Models.Tests.Packages;

public sealed class PackageExcludeTests : TestBase
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConstructorSetsProperties(bool exactMatch)
    {
        const string packageId = "Package.Id";

        PackageExclude exclude = new(packageId: packageId, exactMatch: exactMatch);

        Assert.Equal(expected: packageId, actual: exclude.PackageId);
        Assert.Equal(expected: exactMatch, actual: exclude.ExactMatch);
    }
}
