using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Tests;

public sealed class DotNetVersionTests : LoggingTestBase
{
    private readonly IDotNetVersion _dotNetVersion;

    public DotNetVersionTests(ITestOutputHelper output)
        : base(output)
    {
        this._dotNetVersion = new DotNetVersion();
    }

    [Fact]
    public async Task MustReturnVersionsAsync()
    {
        IReadOnlyList<Version> versions = await this._dotNetVersion.GetInstalledSdksAsync(
            this.CancellationToken()
        );
        Assert.NotEmpty(versions);

        foreach (Version version in versions)
        {
            this.Output.WriteLine($"* {version}");
        }
    }
}
