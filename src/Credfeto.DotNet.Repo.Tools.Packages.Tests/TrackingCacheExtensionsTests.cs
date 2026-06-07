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

namespace Credfeto.DotNet.Repo.Tools.Packages.Tests;

public sealed class TrackingCacheExtensionsTests : TestBase
{
    private const string CLONE_PATH = "git@github.com:test/test-repo.git";
    private const string DEFAULT_BRANCH = "main";
    private const string TRACKING_FILE = "/tmp/tracking.json";

    private static readonly DotNetVersionSettings DefaultDotNetSettings = new(
        SdkVersion: null,
        AllowPreRelease: false,
        RollForward: "latestMajor"
    );

    private static readonly ReleaseConfig DefaultReleaseConfig = new(
        AutoReleasePendingPackages: 1,
        MinimumHoursBeforeAutoRelease: 5,
        InactivityHoursBeforeAutoRelease: 9,
        NeverRelease: [],
        AllowedAutoUpgrade: [],
        AlwaysMatch: []
    );

    private readonly IGitRepository _repository;
    private readonly RepoContext _repoContext;
    private readonly ITrackingCache _trackingCache;

    public TrackingCacheExtensionsTests()
    {
        this._trackingCache = GetSubstitute<ITrackingCache>();
        this._repository = GetSubstitute<IGitRepository>();
        this._repository.ClonePath.Returns(CLONE_PATH);
        this._repository.WorkingDirectory.Returns("/tmp/work");
        this._repository.GetDefaultBranch(GitConstants.Upstream).Returns(DEFAULT_BRANCH);

        this._repoContext = new RepoContext(Repository: this._repository, ChangeLogFileName: "CHANGELOG.md");
    }

    [Fact]
    public async Task UpdateTrackingWithNonEmptyTrackingFileNameCallsSetAndSaveAsync()
    {
        PackageUpdateContext updateContext = CreateUpdateContext(trackingFileName: TRACKING_FILE);

        await this._trackingCache.UpdateTrackingAsync(
            repoContext: this._repoContext,
            updateContext: updateContext,
            value: "some-value",
            cancellationToken: this.CancellationToken()
        );

        this._trackingCache.Received(1).Set(repoUrl: CLONE_PATH, value: "some-value");
        await this
            ._trackingCache.Received(1)
            .SaveAsync(fileName: TRACKING_FILE, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTrackingWithEmptyTrackingFileNameCallsSetButNotSaveAsync()
    {
        PackageUpdateContext updateContext = CreateUpdateContext(trackingFileName: string.Empty);

        await this._trackingCache.UpdateTrackingAsync(
            repoContext: this._repoContext,
            updateContext: updateContext,
            value: "some-value",
            cancellationToken: this.CancellationToken()
        );

        this._trackingCache.Received(1).Set(repoUrl: CLONE_PATH, value: "some-value");
        await this._trackingCache.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTrackingWithNullValueCallsSetWithNullAsync()
    {
        PackageUpdateContext updateContext = CreateUpdateContext(trackingFileName: TRACKING_FILE);

        await this._trackingCache.UpdateTrackingAsync(
            repoContext: this._repoContext,
            updateContext: updateContext,
            value: null,
            cancellationToken: this.CancellationToken()
        );

        this._trackingCache.Received(1).Set(repoUrl: CLONE_PATH, value: null);
        await this
            ._trackingCache.Received(1)
            .SaveAsync(fileName: TRACKING_FILE, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTrackingWithWhiteSpaceTrackingFileNameCallsSetButNotSaveAsync()
    {
        PackageUpdateContext updateContext = CreateUpdateContext(trackingFileName: "   ");

        await this._trackingCache.UpdateTrackingAsync(
            repoContext: this._repoContext,
            updateContext: updateContext,
            value: "some-value",
            cancellationToken: this.CancellationToken()
        );

        this._trackingCache.Received(1).Set(repoUrl: CLONE_PATH, value: "some-value");
        await this._trackingCache.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static PackageUpdateContext CreateUpdateContext(string trackingFileName)
    {
        return new PackageUpdateContext(
            WorkFolder: "/tmp/work",
            CacheFileName: null,
            TrackingFileName: trackingFileName,
            AdditionalSources: [],
            DotNetSettings: DefaultDotNetSettings,
            ReleaseConfig: DefaultReleaseConfig
        );
    }
}
