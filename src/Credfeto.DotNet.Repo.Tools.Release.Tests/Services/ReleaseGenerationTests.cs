using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Release.Services;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.BuildVersion.Interfaces;
using FunFair.Test.Common;
using LibGit2Sharp;
using NSubstitute;
using NuGet.Versioning;
using Xunit;
using MatchType = Credfeto.DotNet.Repo.Tools.Release.Interfaces.MatchType;

namespace Credfeto.DotNet.Repo.Tools.Release.Tests.Services;

public sealed class ReleaseGenerationTests : LoggingFolderCleanupTestBase
{
    private const string CLONE_PATH = "git@github.com:test/test-repo.git";
    private const string DEFAULT_BRANCH = "main";
    private const string HEAD_REV = "abc123";
    private static readonly DateTimeOffset FixedNow = new(
        year: 2024,
        month: 1,
        day: 15,
        hour: 12,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    private static readonly DotNetVersionSettings DefaultDotNetSettings = new(
        SdkVersion: null,
        AllowPreRelease: false,
        RollForward: "latestMajor"
    );

    private readonly IDotNetBuild _dotNetBuild;
    private readonly IDotNetSolutionCheck _dotNetSolutionCheck;
    private readonly IReleaseGeneration _releaseGeneration;
    private readonly IGitRepository _repository;
    private readonly ICurrentTimeSource _timeSource;
    private readonly ITrackingCache _trackingCache;
    private readonly IVersionDetector _versionDetector;

    public ReleaseGenerationTests(ITestOutputHelper output)
        : base(output)
    {
        this._timeSource = GetSubstitute<ICurrentTimeSource>();
        this._versionDetector = GetSubstitute<IVersionDetector>();
        this._trackingCache = GetSubstitute<ITrackingCache>();
        this._dotNetSolutionCheck = GetSubstitute<IDotNetSolutionCheck>();
        this._dotNetBuild = GetSubstitute<IDotNetBuild>();
        this._repository = GetSubstitute<IGitRepository>();

        this._repository.ClonePath.Returns(CLONE_PATH);
        this._repository.WorkingDirectory.Returns(this.TempFolder);
        this._repository.GetDefaultBranch(GitConstants.Upstream).Returns(DEFAULT_BRANCH);
        this._repository.HeadRev.Returns(HEAD_REV);

        this._releaseGeneration = new ReleaseGeneration(
            timeSource: this._timeSource,
            versionDetector: this._versionDetector,
            trackingCache: this._trackingCache,
            dotNetSolutionCheck: this._dotNetSolutionCheck,
            dotNetBuild: this._dotNetBuild,
            logger: this.GetTypedLogger<ReleaseGeneration>()
        );
    }

    private static ReleaseConfig NeverReleaseConfig()
    {
        return new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 5,
            InactivityHoursBeforeAutoRelease: 9,
            NeverRelease: [new(Repo: CLONE_PATH, MatchType: MatchType.EXACT, Include: true)],
            AllowedAutoUpgrade: [],
            AlwaysMatch: []
        );
    }

    private static ReleaseConfig AlwaysMatchConfig()
    {
        return new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 5,
            InactivityHoursBeforeAutoRelease: 9,
            NeverRelease: [],
            AllowedAutoUpgrade: [],
            AlwaysMatch: [new(Repo: CLONE_PATH, MatchType: MatchType.EXACT, Include: true)]
        );
    }

    private static ReleaseConfig AllowedAutoUpgradeConfig()
    {
        return new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 5,
            InactivityHoursBeforeAutoRelease: 9,
            NeverRelease: [],
            AllowedAutoUpgrade: [new(Repo: CLONE_PATH, MatchType: MatchType.EXACT, Include: true)],
            AlwaysMatch: []
        );
    }

    private static ReleaseConfig AllowedAutoUpgradeWithAlwaysMatchConfig()
    {
        return new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 5,
            InactivityHoursBeforeAutoRelease: 9,
            NeverRelease: [],
            AllowedAutoUpgrade: [new(Repo: CLONE_PATH, MatchType: MatchType.EXACT, Include: true)],
            AlwaysMatch: [new(Repo: CLONE_PATH, MatchType: MatchType.EXACT, Include: true)]
        );
    }

    private static ReleaseConfig EmptyPolicyConfig()
    {
        return new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 5,
            InactivityHoursBeforeAutoRelease: 9,
            NeverRelease: [],
            AllowedAutoUpgrade: [],
            AlwaysMatch: []
        );
    }

    private static ReleaseConfig ExplicitlyProhibitedConfig()
    {
        // AllowedAutoUpgrade with Include=false explicitly excludes the repo, causing EXPLICITLY_PROHIBITED
        return new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 5,
            InactivityHoursBeforeAutoRelease: 9,
            NeverRelease: [],
            AllowedAutoUpgrade: [new(Repo: CLONE_PATH, MatchType: MatchType.EXACT, Include: false)],
            AlwaysMatch: []
        );
    }

    private async Task<string> CreateChangelogFileAsync(string content)
    {
        string changelogPath = Path.Combine(path1: this.TempFolder, path2: "CHANGELOG.md");
        await File.WriteAllTextAsync(
            path: changelogPath,
            contents: content,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken: this.CancellationToken()
        );

        return changelogPath;
    }

    private static string ChangelogWithNoDependencyUpdates()
    {
        return """
            # Changelog
            All notable changes to this project will be documented in this file.

            ## [Unreleased]
            ### Changed

            ## [0.0.1] - 2024-01-01
            ### Changed
            - Initial release
            """;
    }

    private static string ChangelogWithTwoDependencyUpdates()
    {
        return """
            # Changelog
            All notable changes to this project will be documented in this file.

            ## [Unreleased]
            ### Changed
            - Dependencies - Updated SomePackage to 2.0.0
            - Dependencies - Updated OtherPackage to 3.0.0

            ## [0.0.1] - 2024-01-01
            ### Changed
            - Initial release
            """;
    }

    private static string ChangelogWithOneDependencyUpdate()
    {
        return """
            # Changelog
            All notable changes to this project will be documented in this file.

            ## [Unreleased]
            ### Changed
            - Dependencies - Updated SomePackage to 2.0.0

            ## [0.0.1] - 2024-01-01
            ### Changed
            - Initial release
            """;
    }

    private static string ChangelogWithGeoIpUpdate()
    {
        return """
            # Changelog
            All notable changes to this project will be documented in this file.

            ## [Unreleased]
            ### Changed
            - GEOIP - Updated GeoIP database

            ## [0.0.1] - 2024-01-01
            ### Changed
            - Initial release
            """;
    }

    private static string ChangelogWithDotNetSdkUpdate()
    {
        return """
            # Changelog
            All notable changes to this project will be documented in this file.

            ## [Unreleased]
            ### Changed
            - SDK - Updated DotNet SDK to 9.0.100

            ## [0.0.1] - 2024-01-01
            ### Changed
            - Initial release
            """;
    }

    private static string ChangelogWithMatchedPackageUpdate()
    {
        return """
            # Changelog
            All notable changes to this project will be documented in this file.

            ## [Unreleased]
            ### Changed
            - Dependencies - Updated SomePackage to 2.0.0

            ## [0.0.1] - 2024-01-01
            ### Changed
            - Initial release
            """;
    }

    private static string ChangelogWithReleaseSections()
    {
        return """
            # Changelog
            All notable changes to this project will be documented in this file.

            ## [Unreleased]
            ### Changed
            - Dependencies - Updated SomePackage to 2.0.0
            - Dependencies - Updated OtherPackage to 3.0.0

            ## [1.0.0] - 2024-01-01
            ### Changed
            - Initial release
            """;
    }

    private RepoContext CreateRepoContext(string changelogPath)
    {
        return new RepoContext(
            ClonePath: CLONE_PATH,
            Repository: this._repository,
            WorkingDirectory: this.TempFolder,
            DefaultBranch: DEFAULT_BRANCH,
            ChangeLogFileName: changelogPath
        );
    }

    private static DotNetFiles EmptyDotNetFiles()
    {
        return new DotNetFiles(SourceDirectory: "/src", Solutions: [], Projects: []);
    }

    private static DotNetFiles DotNetFilesWithSolutionsAndProjects()
    {
        return new DotNetFiles(SourceDirectory: "/src", Solutions: ["solution.sln"], Projects: ["project.csproj"]);
    }

    private static BuildSettings EmptyBuildSettings()
    {
        return new BuildSettings(PublishableProjects: [], PackableProjects: [], Framework: null);
    }

    private static BuildSettings PublishableBuildSettings()
    {
        return new BuildSettings(PublishableProjects: ["MyApp.csproj"], PackableProjects: [], Framework: null);
    }

    [Fact]
    public async Task NeverRelease_ReturnsWithoutExceptionAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithNoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        await this._releaseGeneration.TryCreateNextPatchAsync(
            repoContext: repoContext,
            dotNetFiles: EmptyDotNetFiles(),
            buildSettings: EmptyBuildSettings(),
            dotNetSettings: DefaultDotNetSettings,
            packages: [],
            releaseConfig: NeverReleaseConfig(),
            cancellationToken: this.CancellationToken()
        );

        // Should return without throwing - NeverRelease matched
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task EmptyChangelog_SkipsReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithNoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-1));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns([]);

        await this._releaseGeneration.TryCreateNextPatchAsync(
            repoContext: repoContext,
            dotNetFiles: EmptyDotNetFiles(),
            buildSettings: EmptyBuildSettings(),
            dotNetSettings: DefaultDotNetSettings,
            packages: [],
            releaseConfig: EmptyPolicyConfig(),
            cancellationToken: this.CancellationToken()
        );

        // Should return without throwing - empty changelog = EXPLICITLY_PROHIBITED via fuzzy rules
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task InsufficientTime_SkipsReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithTwoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        // Very recent commit - not enough time for auto-release
        this._repository.GetLastCommitDate().Returns(FixedNow.AddMinutes(-5));

        await this._releaseGeneration.TryCreateNextPatchAsync(
            repoContext: repoContext,
            dotNetFiles: EmptyDotNetFiles(),
            buildSettings: EmptyBuildSettings(),
            dotNetSettings: DefaultDotNetSettings,
            packages: [],
            releaseConfig: EmptyPolicyConfig(),
            cancellationToken: this.CancellationToken()
        );

        // Should return without throwing - INSUFFICIENT_DURATION_SINCE_LAST_UPDATE
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task SufficientUpdatesAndTime_WithSolutionCheckFailure_SkipsReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithTwoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-6));

        this._dotNetSolutionCheck.When(async x =>
                await x.ReleaseCheckAsync(
                    Arg.Any<IReadOnlyList<string>>(),
                    Arg.Any<DotNetVersionSettings>(),
                    Arg.Any<CancellationToken>()
                )
            )
            .Do(_ => throw new SolutionCheckFailedException("Solution check failed"));

        await this._releaseGeneration.TryCreateNextPatchAsync(
            repoContext: repoContext,
            dotNetFiles: DotNetFilesWithSolutionsAndProjects(),
            buildSettings: EmptyBuildSettings(),
            dotNetSettings: DefaultDotNetSettings,
            packages: [],
            releaseConfig: EmptyPolicyConfig(),
            cancellationToken: this.CancellationToken()
        );

        // Should return without throwing - FAILED_RELEASE_CHECK
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task SufficientUpdatesAndTime_WithBuildFailure_SkipsReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithTwoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-6));

        this._dotNetBuild.When(async x => await x.BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new DotNetBuildErrorException("Build failed"));

        await this._releaseGeneration.TryCreateNextPatchAsync(
            repoContext: repoContext,
            dotNetFiles: DotNetFilesWithSolutionsAndProjects(),
            buildSettings: EmptyBuildSettings(),
            dotNetSettings: DefaultDotNetSettings,
            packages: [],
            releaseConfig: EmptyPolicyConfig(),
            cancellationToken: this.CancellationToken()
        );

        // Should return without throwing - DOES_NOT_BUILD
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task SufficientUpdatesAndTime_WithPendingDependencyBranches_SkipsReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithTwoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-6));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns(["dependabot/nuget/SomePackage"]);

        await this._releaseGeneration.TryCreateNextPatchAsync(
            repoContext: repoContext,
            dotNetFiles: EmptyDotNetFiles(),
            buildSettings: EmptyBuildSettings(),
            dotNetSettings: DefaultDotNetSettings,
            packages: [],
            releaseConfig: EmptyPolicyConfig(),
            cancellationToken: this.CancellationToken()
        );

        // Should return without throwing - FOUND_PENDING_UPDATE_BRANCHES
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task SufficientUpdatesAndTime_WithAlwaysCreatePatchRelease_CreatesReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithTwoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-6));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns([]);

        NuGetVersion version = new(major: 1, minor: 0, patch: 0);
        this._versionDetector.FindVersion(Arg.Any<Repository>(), Arg.Any<int>()).Returns(version);
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<ReleaseCreatedException>(testCode: async () =>
            await this._releaseGeneration.TryCreateNextPatchAsync(
                repoContext: repoContext,
                dotNetFiles: EmptyDotNetFiles(),
                buildSettings: EmptyBuildSettings(),
                dotNetSettings: DefaultDotNetSettings,
                packages: [],
                releaseConfig: AlwaysMatchConfig(),
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task AllowedAutoUpgrade_NotPublishable_CreatesReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithTwoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-6));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns([]);

        NuGetVersion version = new(major: 1, minor: 0, patch: 0);
        this._versionDetector.FindVersion(Arg.Any<Repository>(), Arg.Any<int>()).Returns(version);
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<ReleaseCreatedException>(testCode: async () =>
            await this._releaseGeneration.TryCreateNextPatchAsync(
                repoContext: repoContext,
                dotNetFiles: EmptyDotNetFiles(),
                buildSettings: EmptyBuildSettings(),
                dotNetSettings: DefaultDotNetSettings,
                packages: [],
                releaseConfig: AllowedAutoUpgradeConfig(),
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task AllowedAutoUpgrade_Publishable_AlwaysRelease_CreatesReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithTwoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-6));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns([]);

        NuGetVersion version = new(major: 1, minor: 0, patch: 0);
        this._versionDetector.FindVersion(Arg.Any<Repository>(), Arg.Any<int>()).Returns(version);
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<ReleaseCreatedException>(testCode: async () =>
            await this._releaseGeneration.TryCreateNextPatchAsync(
                repoContext: repoContext,
                dotNetFiles: EmptyDotNetFiles(),
                buildSettings: PublishableBuildSettings(),
                dotNetSettings: DefaultDotNetSettings,
                packages: [],
                releaseConfig: AllowedAutoUpgradeWithAlwaysMatchConfig(),
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task AllowedAutoUpgrade_Publishable_NotAlwaysRelease_SkipsReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithTwoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-6));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns([]);

        await this._releaseGeneration.TryCreateNextPatchAsync(
            repoContext: repoContext,
            dotNetFiles: EmptyDotNetFiles(),
            buildSettings: PublishableBuildSettings(),
            dotNetSettings: DefaultDotNetSettings,
            packages: [],
            releaseConfig: AllowedAutoUpgradeConfig(),
            cancellationToken: this.CancellationToken()
        );

        // Should return without throwing - CONTAINS_PUBLISHABLE_EXECUTABLES
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task NoPolicy_SkipsReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithTwoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-6));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns([]);

        await this._releaseGeneration.TryCreateNextPatchAsync(
            repoContext: repoContext,
            dotNetFiles: EmptyDotNetFiles(),
            buildSettings: EmptyBuildSettings(),
            dotNetSettings: DefaultDotNetSettings,
            packages: [],
            releaseConfig: ExplicitlyProhibitedConfig(),
            cancellationToken: this.CancellationToken()
        );

        // Should return without throwing - EXPLICITLY_PROHIBITED
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task CreateAsync_ThrowsReleaseCreatedExceptionAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithReleaseSections());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        NuGetVersion version = new(major: 1, minor: 0, patch: 0);
        this._versionDetector.FindVersion(Arg.Any<Repository>(), Arg.Any<int>()).Returns(version);
        // release/1.0.0 exists, so it will try 1.0.1
        this._repository.DoesBranchExist("release/1.0.0").Returns(true);
        this._repository.DoesBranchExist("release/1.0.1").Returns(false);

        await Assert.ThrowsAsync<ReleaseCreatedException>(testCode: async () =>
            await this._releaseGeneration.CreateAsync(
                repoContext: repoContext,
                cancellationToken: this.CancellationToken()
            )
        );

        await this._repository.Received(1).CommitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this._repository.Received(1).PushAsync(Arg.Any<CancellationToken>());
        await this._repository.Received(1).CreateBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this
            ._repository.Received(1)
            .PushOriginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        this._trackingCache.Received(1).Set(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task InactivityPath_CreatesReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithOneDependencyUpdate());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        // Only 1 update (not > AutoReleasePendingPackages=1), but time > InactivityHoursBeforeAutoRelease=9
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-10));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns([]);

        NuGetVersion version = new(major: 1, minor: 0, patch: 0);
        this._versionDetector.FindVersion(Arg.Any<Repository>(), Arg.Any<int>()).Returns(version);
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<ReleaseCreatedException>(testCode: async () =>
            await this._releaseGeneration.TryCreateNextPatchAsync(
                repoContext: repoContext,
                dotNetFiles: EmptyDotNetFiles(),
                buildSettings: EmptyBuildSettings(),
                dotNetSettings: DefaultDotNetSettings,
                packages: [],
                releaseConfig: AlwaysMatchConfig(),
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task GeoIpScore_ContributesToUpdateCountAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithGeoIpUpdate());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        // 1 GeoIP entry -> score=1 which is >= 1, time > InactivityHoursBeforeAutoRelease=9
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-10));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns([]);

        NuGetVersion version = new(major: 1, minor: 0, patch: 0);
        this._versionDetector.FindVersion(Arg.Any<Repository>(), Arg.Any<int>()).Returns(version);
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<ReleaseCreatedException>(testCode: async () =>
            await this._releaseGeneration.TryCreateNextPatchAsync(
                repoContext: repoContext,
                dotNetFiles: EmptyDotNetFiles(),
                buildSettings: EmptyBuildSettings(),
                dotNetSettings: DefaultDotNetSettings,
                packages: [],
                releaseConfig: AlwaysMatchConfig(),
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task DotNetSdkScore_ContributesToUpdateCountAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithDotNetSdkUpdate());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        // SDK = 1000 points which is > AutoReleasePendingPackages=1 AND time > MinimumHoursBeforeAutoRelease=5
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-6));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns([]);

        NuGetVersion version = new(major: 1, minor: 0, patch: 0);
        this._versionDetector.FindVersion(Arg.Any<Repository>(), Arg.Any<int>()).Returns(version);
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<ReleaseCreatedException>(testCode: async () =>
            await this._releaseGeneration.TryCreateNextPatchAsync(
                repoContext: repoContext,
                dotNetFiles: EmptyDotNetFiles(),
                buildSettings: EmptyBuildSettings(),
                dotNetSettings: DefaultDotNetSettings,
                packages: [],
                releaseConfig: AlwaysMatchConfig(),
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task MatchedPackage_ContributesToScoreAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithMatchedPackageUpdate());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        // 1 matched package -> score=1, time > InactivityHoursBeforeAutoRelease=9
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-10));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns([]);

        NuGetVersion version = new(major: 1, minor: 0, patch: 0);
        this._versionDetector.FindVersion(Arg.Any<Repository>(), Arg.Any<int>()).Returns(version);
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(false);

        PackageUpdate matchedPackage = new(
            packageId: "SomePackage",
            packageType: "nuget",
            exactMatch: true,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );

        await Assert.ThrowsAsync<ReleaseCreatedException>(testCode: async () =>
            await this._releaseGeneration.TryCreateNextPatchAsync(
                repoContext: repoContext,
                dotNetFiles: EmptyDotNetFiles(),
                buildSettings: EmptyBuildSettings(),
                dotNetSettings: DefaultDotNetSettings,
                packages: [matchedPackage],
                releaseConfig: AlwaysMatchConfig(),
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UnmatchedPackage_ContributesZeroScoreAsync()
    {
        // 1 dependency update for a package NOT in the packages list -> score=0
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithOneDependencyUpdate());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-1));

        // score=0 so no release
        await this._releaseGeneration.TryCreateNextPatchAsync(
            repoContext: repoContext,
            dotNetFiles: EmptyDotNetFiles(),
            buildSettings: EmptyBuildSettings(),
            dotNetSettings: DefaultDotNetSettings,
            packages: [],
            releaseConfig: EmptyPolicyConfig(),
            cancellationToken: this.CancellationToken()
        );

        // Should return without throwing - INSUFFICIENT_UPDATES
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task DependencyBranchWithDotNetSdkPreview_IsNotPendingBranchAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithTwoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-6));
        // depends/sdk/dotnet/.../preview is NOT a pending branch
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns(["depends/sdk/dotnet/9/preview"]);

        NuGetVersion version = new(major: 1, minor: 0, patch: 0);
        this._versionDetector.FindVersion(Arg.Any<Repository>(), Arg.Any<int>()).Returns(version);
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<ReleaseCreatedException>(testCode: async () =>
            await this._releaseGeneration.TryCreateNextPatchAsync(
                repoContext: repoContext,
                dotNetFiles: EmptyDotNetFiles(),
                buildSettings: EmptyBuildSettings(),
                dotNetSettings: DefaultDotNetSettings,
                packages: [],
                releaseConfig: AlwaysMatchConfig(),
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task SufficientUpdatesAndTime_WithPassingCodeQuality_CreatesReleaseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithTwoDependencyUpdates());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-6));
        this._repository.GetRemoteBranches(GitConstants.Upstream).Returns([]);

        NuGetVersion version = new(major: 1, minor: 0, patch: 0);
        this._versionDetector.FindVersion(Arg.Any<Repository>(), Arg.Any<int>()).Returns(version);
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<ReleaseCreatedException>(testCode: async () =>
            await this._releaseGeneration.TryCreateNextPatchAsync(
                repoContext: repoContext,
                dotNetFiles: DotNetFilesWithSolutionsAndProjects(),
                buildSettings: EmptyBuildSettings(),
                dotNetSettings: DefaultDotNetSettings,
                packages: [],
                releaseConfig: AlwaysMatchConfig(),
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task ProhibitedPackage_ContributesZeroScoreAsync()
    {
        // A package that is prohibited (ProhibitVersionBumpWhenReferenced=true) contributes score=0
        // so it calls LogIgnoredPackage
        string changelogPath = await this.CreateChangelogFileAsync(ChangelogWithOneDependencyUpdate());
        RepoContext repoContext = this.CreateRepoContext(changelogPath);

        this._timeSource.UtcNow().Returns(FixedNow);
        this._repository.GetLastCommitDate().Returns(FixedNow.AddHours(-1));

        PackageUpdate prohibitedPackage = new(
            packageId: "SomePackage",
            packageType: "nuget",
            exactMatch: true,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: true,
            exclude: null
        );

        // score=0 so no release (INSUFFICIENT_UPDATES)
        await this._releaseGeneration.TryCreateNextPatchAsync(
            repoContext: repoContext,
            dotNetFiles: EmptyDotNetFiles(),
            buildSettings: EmptyBuildSettings(),
            dotNetSettings: DefaultDotNetSettings,
            packages: [prohibitedPackage],
            releaseConfig: EmptyPolicyConfig(),
            cancellationToken: this.CancellationToken()
        );
    }
}
