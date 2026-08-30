using System;
using Credfeto.DotNet.Repo.Tools.Build.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Build.Tests.Helpers;

public sealed class TargetFrameworkVersionComparerTests : TestBase
{
    [Theory]
    [InlineData("net9.0", "net10.0", -1)]
    [InlineData("net10.0", "net9.0", 1)]
    [InlineData("net9.0", "net9.0", 0)]
    [InlineData("net8.0", "net9.0", -1)]
    [InlineData("net9.0", "net8.0", 1)]
    [InlineData("net9.0-windows", "net10.0-windows", -1)]
    [InlineData("net10.0-windows", "net9.0-windows", 1)]
    [InlineData("net472", "net10.0", -1)]
    [InlineData("net10.0", "net472", 1)]
    [InlineData("net48", "net9.0", -1)]
    [InlineData("net9.0", "net48", 1)]
    [InlineData("netstandard2.0", "net10.0", -1)]
    [InlineData("net10.0", "netstandard2.0", 1)]
    public static void Compare(string x, string y, int expectedSign)
    {
        int result = TargetFrameworkVersionComparer.Instance.Compare(x: x, y: y);

        Assert.Equal(expected: expectedSign, actual: Math.Sign(result));
    }
}
