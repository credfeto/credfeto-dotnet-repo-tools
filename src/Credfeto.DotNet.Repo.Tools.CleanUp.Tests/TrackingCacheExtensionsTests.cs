using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests;

public sealed class TrackingCacheExtensionsTests : LoggingTestBase
{
    private static readonly DotNetVersionSettings DotNetSettings = new(
        SdkVersion: null,
        AllowPreRelease: false,
        RollForward: "major"
    );

    private static readonly ReleaseConfig ReleaseConfig = new(
        AutoReleasePendingPackages: 0,
        MinimumHoursBeforeAutoRelease: 1,
        InactivityHoursBeforeAutoRelease: 24,
        NeverRelease: [],
        AllowedAutoUpgrade: [],
        AlwaysMatch: []
    );

    public TrackingCacheExtensionsTests(ITestOutputHelper output)
        : base(output) { }

    private static RepoContext BuildRepoContext(string clonePath, string defaultBranch)
    {
        return new RepoContext(
            ClonePath: clonePath,
            Repository: GetSubstitute<IGitRepository>(),
            WorkingDirectory: "/work",
            DefaultBranch: defaultBranch,
            ChangeLogFileName: "CHANGELOG.md"
        );
    }

    [Fact]
    public async Task UpdateTrackingAsyncShouldCallSetAsync()
    {
        ITrackingCache trackingCache = GetSubstitute<ITrackingCache>();
        RepoContext repoContext = BuildRepoContext(clonePath: "/repo", defaultBranch: "main");
        CleanupUpdateContext updateContext = new(
            WorkFolder: "/work",
            TrackingFileName: string.Empty,
            DotNetSettings: DotNetSettings,
            ReleaseConfig: ReleaseConfig
        );

        await trackingCache.UpdateTrackingAsync(
            repoContext: repoContext,
            updateContext: updateContext,
            value: "test-value",
            cancellationToken: this.CancellationToken()
        );

        trackingCache.Received(1).Set(repoUrl: "/repo", value: "test-value");
    }

    [Fact]
    public async Task UpdateTrackingAsyncShouldNotCallSaveAsyncWhenTrackingFileNameIsEmptyAsync()
    {
        ITrackingCache trackingCache = GetSubstitute<ITrackingCache>();
        RepoContext repoContext = BuildRepoContext(clonePath: "/repo", defaultBranch: "main");
        CleanupUpdateContext updateContext = new(
            WorkFolder: "/work",
            TrackingFileName: string.Empty,
            DotNetSettings: DotNetSettings,
            ReleaseConfig: ReleaseConfig
        );

        await trackingCache.UpdateTrackingAsync(
            repoContext: repoContext,
            updateContext: updateContext,
            value: null,
            cancellationToken: this.CancellationToken()
        );

        await trackingCache.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTrackingAsyncShouldNotCallSaveAsyncWhenTrackingFileNameIsWhitespaceAsync()
    {
        ITrackingCache trackingCache = GetSubstitute<ITrackingCache>();
        RepoContext repoContext = BuildRepoContext(clonePath: "/repo", defaultBranch: "main");
        CleanupUpdateContext updateContext = new(
            WorkFolder: "/work",
            TrackingFileName: "   ",
            DotNetSettings: DotNetSettings,
            ReleaseConfig: ReleaseConfig
        );

        await trackingCache.UpdateTrackingAsync(
            repoContext: repoContext,
            updateContext: updateContext,
            value: null,
            cancellationToken: this.CancellationToken()
        );

        await trackingCache.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTrackingAsyncShouldCallSaveAsyncWhenTrackingFileNameIsSetAsync()
    {
        ITrackingCache trackingCache = GetSubstitute<ITrackingCache>();
        RepoContext repoContext = BuildRepoContext(clonePath: "/repo", defaultBranch: "main");
        CleanupUpdateContext updateContext = new(
            WorkFolder: "/work",
            TrackingFileName: "tracking.json",
            DotNetSettings: DotNetSettings,
            ReleaseConfig: ReleaseConfig
        );

        await trackingCache.UpdateTrackingAsync(
            repoContext: repoContext,
            updateContext: updateContext,
            value: "some-value",
            cancellationToken: this.CancellationToken()
        );

        await trackingCache
            .Received(1)
            .SaveAsync(fileName: "tracking.json", cancellationToken: this.CancellationToken());
    }
}
