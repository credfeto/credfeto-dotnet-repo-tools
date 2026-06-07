using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Services;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Tests.Services;

public sealed class DotNetVersionTests : LoggingTestBase
{
    private readonly IDotNetCommandRunner _commandRunner;
    private readonly IDotNetVersion _dotNetVersion;

    public DotNetVersionTests(ITestOutputHelper output)
        : base(output)
    {
        this._commandRunner = GetSubstitute<IDotNetCommandRunner>();
        this._dotNetVersion = new DotNetVersion(this._commandRunner);
    }

    [Fact]
    public async Task MustReturnVersionsWhenSdksInstalledAsync()
    {
        this._commandRunner.RunAsync(arguments: "--list-sdks", Arg.Any<CancellationToken>())
            .Returns((Output: ["9.0.100 [/usr/share/dotnet/sdk]", "10.0.100 [/usr/share/dotnet/sdk]"], ExitCode: 0));

        IReadOnlyList<Version> versions = await this._dotNetVersion.GetInstalledSdksAsync(this.CancellationToken());

        Assert.NotEmpty(versions);
        Assert.Equal(expected: 2, actual: versions.Count);
    }

    [Fact]
    public async Task MustReturnVersionsInDescendingOrderAsync()
    {
        this._commandRunner.RunAsync(arguments: "--list-sdks", Arg.Any<CancellationToken>())
            .Returns((Output: ["9.0.100 [/usr/share/dotnet/sdk]", "10.0.100 [/usr/share/dotnet/sdk]"], ExitCode: 0));

        IReadOnlyList<Version> versions = await this._dotNetVersion.GetInstalledSdksAsync(this.CancellationToken());

        Assert.True(condition: versions[0] >= versions[^1], userMessage: "Versions should be in descending order");
    }

    [Fact]
    public Task MustThrowWhenSdkListCommandFailsAsync()
    {
        this._commandRunner.RunAsync(arguments: "--list-sdks", Arg.Any<CancellationToken>())
            .Returns((Output: ["Error: command failed"], ExitCode: 1));

        return Assert.ThrowsAsync<InvalidOperationException>(() =>
            this._dotNetVersion.GetInstalledSdksAsync(this.CancellationToken()).AsTask()
        );
    }

    [Fact]
    public async Task MustFilterLinesWithNoVersionAsync()
    {
        this._commandRunner.RunAsync(arguments: "--list-sdks", Arg.Any<CancellationToken>())
            .Returns((Output: ["   ", "9.0.100 [/usr/share/dotnet/sdk]"], ExitCode: 0));

        IReadOnlyList<Version> versions = await this._dotNetVersion.GetInstalledSdksAsync(this.CancellationToken());

        Assert.Single(versions);
    }

    [Fact]
    public async Task MustFilterLinesWithInvalidVersionAsync()
    {
        this._commandRunner.RunAsync(arguments: "--list-sdks", Arg.Any<CancellationToken>())
            .Returns((Output: ["Error: something went wrong", "9.0.100 [/usr/share/dotnet/sdk]"], ExitCode: 0));

        IReadOnlyList<Version> versions = await this._dotNetVersion.GetInstalledSdksAsync(this.CancellationToken());

        Assert.Single(versions);
    }
}
