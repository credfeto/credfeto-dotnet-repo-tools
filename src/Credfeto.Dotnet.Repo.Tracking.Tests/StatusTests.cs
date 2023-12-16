using System;
using FunFair.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.Dotnet.Repo.Tracking.Tests;

public sealed class StatusTests : LoggingFolderCleanupTestBase
{
    public StatusTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public void Placeholder()
    {
        Assert.Empty(Array.Empty<string>());
    }
}