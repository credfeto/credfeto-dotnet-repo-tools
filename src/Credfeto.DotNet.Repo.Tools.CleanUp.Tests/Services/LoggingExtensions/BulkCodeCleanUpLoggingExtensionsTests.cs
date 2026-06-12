using System;
using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services.LoggingExtensions;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services.LoggingExtensions;

public sealed class BulkCodeCleanUpLoggingExtensionsTests : LoggingTestBase
{
    private readonly ILogger<BulkCodeCleanUp> _logger;

    public BulkCodeCleanUpLoggingExtensionsTests(ITestOutputHelper output)
        : base(output)
    {
        this._logger = this.GetTypedLogger<BulkCodeCleanUp>();
    }

    private static RepoContext BuildRepoContext()
    {
        return new RepoContext(
            ClonePath: "/repo/clone",
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
    public void LogCommittingToDefaultShouldNotThrow()
    {
        RepoContext repoContext = BuildRepoContext();
        this._logger.LogCommittingToDefault(in repoContext, packageId: "SomePackage", version: "1.0.0");
    }

    [Fact]
    public void LogCommittingToNamedBranchShouldNotThrow()
    {
        RepoContext repoContext = BuildRepoContext();
        this._logger.LogCommittingToNamedBranch(
            in repoContext,
            branch: "feature/update",
            packageId: "SomePackage",
            version: "1.0.0"
        );
    }

    [Fact]
    public void LogSkippingPackageCommitShouldNotThrow()
    {
        RepoContext repoContext = BuildRepoContext();
        this._logger.LogSkippingPackageCommit(
            in repoContext,
            branch: "feature/existing",
            packageId: "SomePackage",
            version: "2.0.0"
        );
    }

    [Fact]
    public void LogMissingSdkShouldNotThrow()
    {
        Version sdkVersion = new(9, 0, 0);
        IReadOnlyList<Version> installedSdks = [new Version(10, 0, 0), new Version(8, 0, 0)];
        this._logger.LogMissingSdk(sdkVersion: sdkVersion, installedSdks: installedSdks);
    }

    [Fact]
    public void LogMissingSdkWithEmptyInstalledSdksShouldNotThrow()
    {
        Version sdkVersion = new(9, 0, 0);
        IReadOnlyList<Version> installedSdks = [];
        this._logger.LogMissingSdk(sdkVersion: sdkVersion, installedSdks: installedSdks);
    }
}
