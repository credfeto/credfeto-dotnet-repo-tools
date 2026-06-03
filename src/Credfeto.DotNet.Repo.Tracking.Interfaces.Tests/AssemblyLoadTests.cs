using System.Reflection;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tracking.Interfaces.Tests;

public sealed class AssemblyLoadTests : TestBase
{
    [Fact]
    public void AssemblyMustBeLoadable()
    {
        Assembly assembly = typeof(ITrackingCache).Assembly;
        Assert.NotNull(assembly);
    }
}
