using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests;

public sealed class TrackingCacheExtensionsTests : TestBase
{
    private const string CLONE_PATH = "git@github.com:test/test-repo.git";
    private const string DEFAULT_BRANCH = "main";
    private const string TRACKING_FILE = "/tmp/tracking.json";

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
    public async Task UpdateTrackingWithTrackingFilenameCallsSaveAsync()
    {
        TemplateUpdateContext updateContext = CreateUpdateContext(trackingFileName: TRACKING_FILE);

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
    public async Task UpdateTrackingWithoutTrackingFilenameDoesNotCallSaveAsync()
    {
        TemplateUpdateContext updateContext = CreateUpdateContext(trackingFileName: string.Empty);

        await this._trackingCache.UpdateTrackingAsync(
            repoContext: this._repoContext,
            updateContext: updateContext,
            value: "some-value",
            cancellationToken: this.CancellationToken()
        );

        this._trackingCache.Received(1).Set(repoUrl: CLONE_PATH, value: "some-value");
        await this._trackingCache.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static TemplateUpdateContext CreateUpdateContext(string trackingFileName)
    {
        return new TemplateUpdateContext(
            WorkFolder: "/tmp/work",
            TemplateFolder: "/tmp/template",
            TrackingFileName: trackingFileName,
            TemplateConfig: CreateMinimalTemplateConfig(),
            DotNetSettings: new DotNetVersionSettings(
                SdkVersion: null,
                AllowPreRelease: false,
                RollForward: "latestMajor"
            ),
            ReleaseConfig: new ReleaseConfig(
                AutoReleasePendingPackages: 0,
                MinimumHoursBeforeAutoRelease: 0,
                InactivityHoursBeforeAutoRelease: 0,
                NeverRelease: [],
                AllowedAutoUpgrade: [],
                AlwaysMatch: []
            )
        );
    }

    private static TemplateConfig CreateMinimalTemplateConfig()
    {
        return new TemplateConfig(
            general: new GeneralTemplateConfig(files: new Dictionary<string, string>(System.StringComparer.Ordinal)),
            gitHub: new GitHubTemplateConfig(
                issueTemplates: false,
                pullRequestTemplates: false,
                actions: false,
                linters: false,
                files: new Dictionary<string, string>(System.StringComparer.Ordinal),
                dependabot: new DependabotTemplateConfig(generate: false),
                labels: new LabelsTemplateConfig(generate: false)
            ),
            dotNet: new DotnetTemplateConfig(
                globalJson: false,
                jetBrainsDotSettings: false,
                files: new Dictionary<string, string>(System.StringComparer.Ordinal)
            ),
            cleanup: new CleanupTemplateConfig(files: new Dictionary<string, string>(System.StringComparer.Ordinal))
        );
    }
}
