using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Credfeto.Tsql.Formatter;
using FunFair.Test.Common;
using LibGit2Sharp;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

public sealed class BulkCodeCleanUpTests : LoggingFolderCleanupTestBase
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

    private readonly IDotNetBuild _dotNetBuild;
    private readonly IDotNetFilesDetector _dotNetFilesDetector;
    private readonly IDotNetVersion _dotNetVersion;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGlobalJson _globalJson;
    private readonly IProjectXmlRewriter _projectXmlRewriter;
    private readonly IResharperSuppressionToSuppressMessage _resharperSuppressionToSuppressMessage;
    private readonly IBulkCodeCleanUp _sut;
    private readonly ISourceFileReformatter _sourceFileReformatter;
    private readonly ISourceFileSuppressionRemover _sourceFileSuppressionRemover;
    private readonly ITrackingCache _trackingCache;
    private readonly IReleaseConfigLoader _releaseConfigLoader;
    private readonly ITransactSqlFormatter _tsqlFormatter;
    private readonly IXmlDocCommentRemover _xmlDocCommentRemover;

    public BulkCodeCleanUpTests(ITestOutputHelper output)
        : base(output)
    {
        this._trackingCache = GetSubstitute<ITrackingCache>();
        this._gitRepositoryFactory = GetSubstitute<IGitRepositoryFactory>();
        this._globalJson = GetSubstitute<IGlobalJson>();
        this._dotNetVersion = GetSubstitute<IDotNetVersion>();
        this._releaseConfigLoader = GetSubstitute<IReleaseConfigLoader>();
        this._dotNetBuild = GetSubstitute<IDotNetBuild>();
        this._dotNetFilesDetector = GetSubstitute<IDotNetFilesDetector>();
        this._projectXmlRewriter = GetSubstitute<IProjectXmlRewriter>();
        this._tsqlFormatter = GetSubstitute<ITransactSqlFormatter>();
        this._sourceFileReformatter = GetSubstitute<ISourceFileReformatter>();
        this._xmlDocCommentRemover = GetSubstitute<IXmlDocCommentRemover>();
        this._resharperSuppressionToSuppressMessage = GetSubstitute<IResharperSuppressionToSuppressMessage>();
        this._sourceFileSuppressionRemover = GetSubstitute<ISourceFileSuppressionRemover>();

        this._globalJson.LoadGlobalJsonAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(DotNetSettings);
        this._dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<System.Version>>([]);
        this._releaseConfigLoader.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ReleaseConfig);

        this._sut = new BulkCodeCleanUp(
            trackingCache: this._trackingCache,
            gitRepositoryFactory: this._gitRepositoryFactory,
            globalJson: this._globalJson,
            dotNetFilesDetector: this._dotNetFilesDetector,
            dotNetVersion: this._dotNetVersion,
            releaseConfigLoader: this._releaseConfigLoader,
            projectXmlRewriter: this._projectXmlRewriter,
            sourceFileReformatter: this._sourceFileReformatter,
            xmlDocCommentRemover: this._xmlDocCommentRemover,
            resharperSuppressionToSuppressMessage: this._resharperSuppressionToSuppressMessage,
            sourceFileSuppressionRemover: this._sourceFileSuppressionRemover,
            tsqlFormatter: this._tsqlFormatter,
            dotNetBuild: this._dotNetBuild,
            logger: this.GetTypedLogger<BulkCodeCleanUp>()
        );
    }

    private void SetupTemplateRepo(IGitRepository templateRepo)
    {
        templateRepo.WorkingDirectory.Returns(this.TempFolder);
        this._gitRepositoryFactory.OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(templateRepo);
    }

    private void SetupTemplateRepoThrowingOnSubsequentCalls<TException>(IGitRepository templateRepo, TException ex)
        where TException : Exception
    {
        templateRepo.WorkingDirectory.Returns(this.TempFolder);
        this._gitRepositoryFactory.OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(templateRepo);

        int callCount = 0;
        this._gitRepositoryFactory.When(f =>
                f.OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            )
            .Do(_ =>
            {
                ++callCount;

                if (callCount > 1)
                {
                    throw ex;
                }
            });
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldSucceedWithNoRepositoriesAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepo(templateRepo);

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: [],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldLoadAndSaveTrackingCacheWhenFileExistsAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepo(templateRepo);

        string trackingFile = Path.Combine(path1: this.TempFolder, path2: "tracking.json");
        await File.WriteAllTextAsync(path: trackingFile, contents: "{}", cancellationToken: this.CancellationToken());

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: trackingFile,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this
            ._trackingCache.Received(1)
            .LoadAsync(fileName: trackingFile, cancellationToken: Arg.Any<CancellationToken>());
        await this
            ._trackingCache.Received(1)
            .SaveAsync(fileName: trackingFile, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldNotLoadTrackingCacheWhenFileDoesNotExistAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepo(templateRepo);

        string trackingFile = Path.Combine(path1: this.TempFolder, path2: "nonexistent-tracking.json");

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: trackingFile,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this._trackingCache.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this
            ._trackingCache.Received(1)
            .SaveAsync(fileName: trackingFile, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldHandleGitRepositoryLockedExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepoThrowingOnSubsequentCalls(
            templateRepo: templateRepo,
            new GitRepositoryLockedException("Repository is locked")
        );

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: ["https://github.com/test/locked-repo.git"],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldHandleSolutionCheckFailedExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepoThrowingOnSubsequentCalls(
            templateRepo: templateRepo,
            new SolutionCheckFailedException("Solution check failed")
        );

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: ["https://github.com/test/failing-solution-repo.git"],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldHandleDotNetBuildErrorExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepoThrowingOnSubsequentCalls(
            templateRepo: templateRepo,
            new DotNetBuildErrorException("Build failed")
        );

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: ["https://github.com/test/build-error-repo.git"],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldAbortRunOnReleaseCreatedExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepoThrowingOnSubsequentCalls(
            templateRepo: templateRepo,
            new ReleaseCreatedException("Release was created")
        );

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: ["https://github.com/test/release-repo.git"],
            cancellationToken: this.CancellationToken()
        );
    }

    private void SetupTwoRepos(IGitRepository templateRepo, IGitRepository testRepo)
    {
        templateRepo.WorkingDirectory.Returns(this.TempFolder);
        this._gitRepositoryFactory.OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(templateRepo);

        int callCount = 0;
        this._gitRepositoryFactory.When(f =>
                f.OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            )
            .Do(_ =>
            {
                ++callCount;

                if (callCount == 2)
                {
                    this._gitRepositoryFactory.OpenOrCloneAsync(
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<CancellationToken>()
                        )
                        .Returns(testRepo);
                }
            });
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldProcessRepoWithNoChangelogAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-nochangelog");
        Directory.CreateDirectory(repoDir);
        Repository.Init(repoDir);

        using Repository activeRepo = new(repoDir);

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns("https://github.com/test/noop-repo.git");
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");
        testRepo.HeadRev.Returns("abc123");

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: ["https://github.com/test/noop-repo.git"],
            cancellationToken: this.CancellationToken()
        );

        this._trackingCache.Received(1).Set(repoUrl: "https://github.com/test/noop-repo.git", value: "abc123");
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldProcessRepoWithChangelogButNoDotNetFilesAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-changelog");
        Directory.CreateDirectory(repoDir);
        Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog\n",
            cancellationToken: this.CancellationToken()
        );

        using Repository activeRepo = new(repoDir);

        this._dotNetFilesDetector.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [], Projects: []));

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns("https://github.com/test/changelog-repo.git");
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");
        testRepo.HeadRev.Returns("def456");

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: ["https://github.com/test/changelog-repo.git"],
            cancellationToken: this.CancellationToken()
        );

        this._trackingCache.Received(1).Set(repoUrl: "https://github.com/test/changelog-repo.git", value: "def456");
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldProcessRepoWithChangelogAndDotNetFilesAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-dotnet");
        Directory.CreateDirectory(repoDir);
        Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog\n",
            cancellationToken: this.CancellationToken()
        );

        string projectFile = Path.Combine(repoDir, "Test.csproj");
        const string projectXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";
        await File.WriteAllTextAsync(
            path: projectFile,
            contents: projectXml,
            cancellationToken: this.CancellationToken()
        );

        const string sqlContent = "SELECT 1;";
        string sqlFile = Path.Combine(repoDir, "schema.sql");
        await File.WriteAllTextAsync(path: sqlFile, contents: sqlContent, cancellationToken: this.CancellationToken());

        using Repository activeRepo = new(repoDir);

        this._dotNetFilesDetector.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [projectFile], Projects: [projectFile]));
        this._dotNetBuild.LoadBuildSettingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new BuildSettings(PublishableProjects: [], PackableProjects: [], Framework: null));
        this._dotNetBuild.When(async b => await b.BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => { });

        this._tsqlFormatter.FormatAsync(
                Arg.Any<string>(),
                Arg.Any<SqlScriptGeneratorOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(sqlContent);

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns("https://github.com/test/dotnet-repo.git");
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");
        testRepo.HeadRev.Returns("ghi789");

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: ["https://github.com/test/dotnet-repo.git"],
            cancellationToken: this.CancellationToken()
        );

        this._trackingCache.Received(1).Set(repoUrl: "https://github.com/test/dotnet-repo.git", value: "ghi789");
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldCommitWhenProjectFileChangedAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-changes");
        Directory.CreateDirectory(repoDir);
        Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog\n",
            cancellationToken: this.CancellationToken()
        );

        string projectFile = Path.Combine(repoDir, "Test.csproj");
        const string projectXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Library</OutputType></PropertyGroup></Project>";
        await File.WriteAllTextAsync(
            path: projectFile,
            contents: projectXml,
            cancellationToken: this.CancellationToken()
        );

        using Repository activeRepo = new(repoDir);

        this._dotNetFilesDetector.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [projectFile], Projects: [projectFile]));
        this._dotNetBuild.LoadBuildSettingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new BuildSettings(PublishableProjects: [], PackableProjects: [], Framework: null));
        this._dotNetBuild.When(async b => await b.BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => { });

        this._projectXmlRewriter.ReOrderPropertyGroups(Arg.Any<System.Xml.XmlDocument>(), Arg.Any<string>())
            .Returns(true);

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns("https://github.com/test/changes-repo.git");
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");
        testRepo.HeadRev.Returns("jkl012");

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: ["https://github.com/test/changes-repo.git"],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldSaveTrackingFileAfterLockedRepoExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepoThrowingOnSubsequentCalls(
            templateRepo: templateRepo,
            new GitRepositoryLockedException("Repository is locked")
        );

        string trackingFile = Path.Combine(path1: this.TempFolder, path2: "tracking-for-exception.json");
        await File.WriteAllTextAsync(path: trackingFile, contents: "{}", cancellationToken: this.CancellationToken());

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: trackingFile,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: ["https://github.com/test/locked-repo-2.git"],
            cancellationToken: this.CancellationToken()
        );

        await this
            ._trackingCache.Received(2)
            .SaveAsync(fileName: trackingFile, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public Task BulkUpdateAsyncShouldThrowWhenSdkVersionNotInstalledAsync()
    {
        this._globalJson.LoadGlobalJsonAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DotNetVersionSettings(SdkVersion: "99.0.0", AllowPreRelease: false, RollForward: "major"));
        this._dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<System.Version>>([new System.Version(10, 0, 0)]);

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepo(templateRepo);

        return Assert.ThrowsAsync<DotNetBuildErrorException>(() =>
            this
                ._sut.BulkUpdateAsync(
                    templateRepository: "https://github.com/template/repo.git",
                    trackingFileName: string.Empty,
                    packagesFileName: string.Empty,
                    workFolder: this.TempFolder,
                    releaseConfigFileName: "release.json",
                    repositories: [],
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }
}
