using System;
using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services.LoggingExtensions;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Services.LoggingExtensions;

public sealed class BulkDependencyUpdaterLoggingExtensionsTests : LoggingTestBase
{
    private readonly ILogger<BulkDependencyReducer> _logger;

    public BulkDependencyUpdaterLoggingExtensionsTests(ITestOutputHelper output)
        : base(output)
    {
        this._logger = this.GetTypedLogger<BulkDependencyReducer>();
    }

    private static RepoContext BuildRepoContext()
    {
        return new RepoContext(
            ClonePath: "https://github.com/test/repo.git",
            Repository: GetSubstitute<IGitRepository>(),
            WorkingDirectory: "/repo/work",
            DefaultBranch: "main",
            ChangeLogFileName: "CHANGELOG.md"
        );
    }

    [Fact]
    public void LogResettingToDefaultShouldNotThrow()
    {
        RepoContext repoContext = BuildRepoContext();
        this._logger.LogResettingToDefault(in repoContext);
    }

    [Fact]
    public void LogMissingSdkWithMultipleInstalledSdksShouldNotThrow()
    {
        Version sdkVersion = new(9, 0, 305);
        IReadOnlyList<Version> installedSdks = [new Version(10, 0, 100), new Version(8, 0, 400)];
        this._logger.LogMissingSdk(sdkVersion: sdkVersion, installedSdks: installedSdks);
    }

    [Fact]
    public void LogMissingSdkWithEmptyInstalledSdksShouldNotThrow()
    {
        Version sdkVersion = new(9, 0, 305);
        IReadOnlyList<Version> installedSdks = [];
        this._logger.LogMissingSdk(sdkVersion: sdkVersion, installedSdks: installedSdks);
    }
}
