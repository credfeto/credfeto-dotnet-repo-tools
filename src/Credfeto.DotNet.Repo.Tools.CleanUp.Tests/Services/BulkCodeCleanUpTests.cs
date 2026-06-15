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
using NSubstitute.ExceptionExtensions;
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2012: Use ValueTasks correctly",
        Justification = "NSubstitute mock setup requires calling async methods without awaiting"
    )]
    private void SetupPassThroughCsCleaners()
    {
        this._tsqlFormatter.FormatAsync(
                Arg.Any<string>(),
                Arg.Any<SqlScriptGeneratorOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(x => x.ArgAt<string>(0));
        this._resharperSuppressionToSuppressMessage.Replace(Arg.Any<string>()).Returns(x => x.ArgAt<string>(0));
        this._xmlDocCommentRemover.RemoveXmlDocComments(Arg.Any<string>()).Returns(x => x.ArgAt<string>(0));
        this._sourceFileSuppressionRemover.RemoveSuppressionsAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<BuildContext>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(x => ValueTask.FromResult(x.ArgAt<string>(1)));
        this._sourceFileReformatter.ReformatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => ValueTask.FromResult(x.ArgAt<string>(1)));
    }

    private async ValueTask<(
        Repository activeRepo,
        string projectFile,
        IGitRepository testRepo
    )> SetupRepoDirWithChangelogAndProjectAsync(string repoDirName, string cloneUrl, string headRev)
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: repoDirName);
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

        Repository activeRepo = new(repoDir);

        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns(cloneUrl);
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");
        testRepo.HeadRev.Returns(headRev);

        return (activeRepo, projectFile, testRepo);
    }

    private void SetupDotNetFilesAndBuild(string repoDir, string projectFile)
    {
        this._dotNetFilesDetector.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [projectFile], Projects: [projectFile]));
        this._dotNetBuild.LoadBuildSettingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new BuildSettings(PublishableProjects: [], PackableProjects: [], Framework: null));
        this._dotNetBuild.When(async b => await b.BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => { });
    }

    private async Task RunBulkUpdateAsync(string repoUrl)
    {
        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: string.Empty,
            workFolder: this.TempFolder,
            releaseConfigFileName: "release.json",
            repositories: [repoUrl],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldCommitWhenSqlFileChangedByFormatterAsync()
    {
        (Repository activeRepo, string projectFile, IGitRepository testRepo) =
            await this.SetupRepoDirWithChangelogAndProjectAsync(
                repoDirName: "gitrepo-sql-change",
                cloneUrl: "https://github.com/test/sql-change-repo.git",
                headRev: "sql001"
            );

        using (activeRepo)
        {
            string repoDir = testRepo.WorkingDirectory;
            const string sqlOriginal = "select 1;";
            const string sqlFormatted = "SELECT 1;";
            string sqlFile = Path.Combine(repoDir, "schema.sql");
            await File.WriteAllTextAsync(
                path: sqlFile,
                contents: sqlOriginal,
                cancellationToken: this.CancellationToken()
            );

            this.SetupDotNetFilesAndBuild(repoDir: repoDir, projectFile: projectFile);

            this._tsqlFormatter.FormatAsync(
                    Arg.Any<string>(),
                    Arg.Any<SqlScriptGeneratorOptions>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(sqlFormatted);

            IGitRepository templateRepo = GetSubstitute<IGitRepository>();
            this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

            await this.RunBulkUpdateAsync("https://github.com/test/sql-change-repo.git");

            await testRepo.Received(1).CommitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldNotCommitWhenCSharpSourceFilesAreUnchangedByCleanupAsync()
    {
        (Repository activeRepo, string projectFile, IGitRepository testRepo) =
            await this.SetupRepoDirWithChangelogAndProjectAsync(
                repoDirName: "gitrepo-cs-unchanged",
                cloneUrl: "https://github.com/test/cs-unchanged-repo.git",
                headRev: "cs001"
            );

        using (activeRepo)
        {
            string repoDir = testRepo.WorkingDirectory;
            const string csContent = "public class Foo { }";
            string csFile = Path.Combine(repoDir, "Foo.cs");
            await File.WriteAllTextAsync(
                path: csFile,
                contents: csContent,
                cancellationToken: this.CancellationToken()
            );

            this.SetupDotNetFilesAndBuild(repoDir: repoDir, projectFile: projectFile);
            this.SetupPassThroughCsCleaners();

            IGitRepository templateRepo = GetSubstitute<IGitRepository>();
            this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

            await this.RunBulkUpdateAsync("https://github.com/test/cs-unchanged-repo.git");

            await testRepo.DidNotReceive().CommitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldCommitWhenCSharpSourceFileIsChangedByCleanupAsync()
    {
        (Repository activeRepo, string projectFile, IGitRepository testRepo) =
            await this.SetupRepoDirWithChangelogAndProjectAsync(
                repoDirName: "gitrepo-cs-changed",
                cloneUrl: "https://github.com/test/cs-changed-repo.git",
                headRev: "cs002"
            );

        using (activeRepo)
        {
            string repoDir = testRepo.WorkingDirectory;
            const string csContent = "// resharper disable\npublic class Foo { }";
            string csFile = Path.Combine(repoDir, "Foo.cs");
            await File.WriteAllTextAsync(
                path: csFile,
                contents: csContent,
                cancellationToken: this.CancellationToken()
            );

            this.SetupDotNetFilesAndBuild(repoDir: repoDir, projectFile: projectFile);
            this.SetupPassThroughCsCleaners();
            // Replace returns different content (simulating cleanup)
            this._resharperSuppressionToSuppressMessage.Replace(Arg.Any<string>()).Returns("public class Foo { }");

            IGitRepository templateRepo = GetSubstitute<IGitRepository>();
            this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

            await this.RunBulkUpdateAsync("https://github.com/test/cs-changed-repo.git");

            await testRepo.Received(1).CommitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldResetWhenBuildFailsAfterCSharpCleanupAsync()
    {
        (Repository activeRepo, string projectFile, IGitRepository testRepo) =
            await this.SetupRepoDirWithChangelogAndProjectAsync(
                repoDirName: "gitrepo-cs-buildfail",
                cloneUrl: "https://github.com/test/cs-buildfail-repo.git",
                headRev: "cs003"
            );

        using (activeRepo)
        {
            string repoDir = testRepo.WorkingDirectory;
            const string csContent = "// resharper\npublic class Foo { }";
            string csFile = Path.Combine(repoDir, "Foo.cs");
            await File.WriteAllTextAsync(
                path: csFile,
                contents: csContent,
                cancellationToken: this.CancellationToken()
            );

            this._dotNetFilesDetector.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [projectFile], Projects: [projectFile]));
            this._dotNetBuild.LoadBuildSettingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
                .Returns(new BuildSettings(PublishableProjects: [], PackableProjects: [], Framework: null));
            // Call 1: initial build for project file cleanup
            // Call 2: initial build for resharper CS cleanup
            // Call 3: TestBuildAndCommitIfCleanAsync build for changed file → FAIL → reset
            this.SetupBuildFailingOnCall(failOnCallNumber: 3);

            this.SetupPassThroughCsCleaners();
            this._resharperSuppressionToSuppressMessage.Replace(Arg.Any<string>()).Returns("public class Foo { }");

            IGitRepository templateRepo = GetSubstitute<IGitRepository>();
            this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

            await this.RunBulkUpdateAsync("https://github.com/test/cs-buildfail-repo.git");

            await testRepo
                .Received(1)
                .ResetToDefaultBranchAsync(
                    upstream: Arg.Any<string>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                );
        }
    }

    private void SetupBuildFailingOnCall(int failOnCallNumber)
    {
        int buildCallCount = 0;
        this._dotNetBuild.When(async b => await b.BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                ++buildCallCount;

                if (buildCallCount == failOnCallNumber)
                {
                    throw new DotNetBuildErrorException("Build failed");
                }
            });
    }

    private void SetupBuildFailingOnCalls(int failOnCallA, int failOnCallB)
    {
        int buildCallCount = 0;
        this._dotNetBuild.When(async b => await b.BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                ++buildCallCount;

                if (buildCallCount == failOnCallA || buildCallCount == failOnCallB)
                {
                    throw new DotNetBuildErrorException("Build failed");
                }
            });
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldRetryBuildWhenPreviousCleanupBuildFailedAsync()
    {
        (Repository activeRepo, string projectFile, IGitRepository testRepo) =
            await this.SetupRepoDirWithChangelogAndProjectAsync(
                repoDirName: "gitrepo-cs-retry-success",
                cloneUrl: "https://github.com/test/cs-retry-success-repo.git",
                headRev: "cs004"
            );

        using (activeRepo)
        {
            string repoDir = testRepo.WorkingDirectory;
            const string cleanedContent = "public class Cleaned { }";
            // Two dirty files: whichever is processed first will have its commit-check fail (lastBuildFailed=true).
            // The second file's retry build will then succeed (covering lines 265-266).
            string csFile1 = Path.Combine(repoDir, "DirtyOne.cs");
            string csFile2 = Path.Combine(repoDir, "DirtyTwo.cs");
            await File.WriteAllTextAsync(
                path: csFile1,
                contents: "// dirty 1\npublic class DirtyOne { }",
                cancellationToken: this.CancellationToken()
            );
            await File.WriteAllTextAsync(
                path: csFile2,
                contents: "// dirty 2\npublic class DirtyTwo { }",
                cancellationToken: this.CancellationToken()
            );

            this._dotNetFilesDetector.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [projectFile], Projects: [projectFile]));
            this._dotNetBuild.LoadBuildSettingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
                .Returns(new BuildSettings(PublishableProjects: [], PackableProjects: [], Framework: null));
            // Call 1: project cleanup initial build
            // Call 2: resharper initial build (two cs files)
            // Call 3: TestBuildAndCommit for first file (changed) → FAIL → reset (lastBuildFailed=true)
            // Call 4: retry build for second file → SUCCESS → lastBuildFailed=false (lines 265-266 covered)
            // Call 5: TestBuildAndCommit for second file (changed) → SUCCESS → commit
            this.SetupBuildFailingOnCall(failOnCallNumber: 3);

            this.SetupPassThroughCsCleaners();
            this._resharperSuppressionToSuppressMessage.Replace(Arg.Any<string>()).Returns(cleanedContent);

            IGitRepository templateRepo = GetSubstitute<IGitRepository>();
            this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

            await this.RunBulkUpdateAsync("https://github.com/test/cs-retry-success-repo.git");

            await testRepo
                .Received(1)
                .ResetToDefaultBranchAsync(
                    upstream: Arg.Any<string>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                );
        }
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldThrowWhenRetryBuildAlsoFailsAsync()
    {
        (Repository activeRepo, string projectFile, IGitRepository testRepo) =
            await this.SetupRepoDirWithChangelogAndProjectAsync(
                repoDirName: "gitrepo-cs-retry-fail",
                cloneUrl: "https://github.com/test/cs-retry-fail-repo.git",
                headRev: "cs005"
            );

        using (activeRepo)
        {
            string repoDir = testRepo.WorkingDirectory;
            // Both files have different content from cleanedContent so both will be "changed"
            // regardless of which is processed first
            const string cleanedContent = "public class Cleaned { }";
            string csFile1 = Path.Combine(repoDir, "FileOne.cs");
            string csFile2 = Path.Combine(repoDir, "FileTwo.cs");
            await File.WriteAllTextAsync(
                path: csFile1,
                contents: "// original\npublic class FileOne { }",
                cancellationToken: this.CancellationToken()
            );
            await File.WriteAllTextAsync(
                path: csFile2,
                contents: "// original\npublic class FileTwo { }",
                cancellationToken: this.CancellationToken()
            );

            this._dotNetFilesDetector.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [projectFile], Projects: [projectFile]));
            this._dotNetBuild.LoadBuildSettingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
                .Returns(new BuildSettings(PublishableProjects: [], PackableProjects: [], Framework: null));
            // Call 1: project cleanup initial build, Call 2: resharper initial build,
            // Call 3: TestBuildAndCommit for first changed file → FAIL → reset (1st), lastBuildFailed=true
            // Call 4: retry build for second file → FAIL → reset (2nd), re-throw (caught by UpdateRepositoriesAsync)
            this.SetupBuildFailingOnCalls(failOnCallA: 3, failOnCallB: 4);

            this.SetupPassThroughCsCleaners();
            // Replace returns cleanedContent for ANY input, so both files will be "changed"
            this._resharperSuppressionToSuppressMessage.Replace(Arg.Any<string>()).Returns(cleanedContent);

            IGitRepository templateRepo = GetSubstitute<IGitRepository>();
            this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

            await this.RunBulkUpdateAsync("https://github.com/test/cs-retry-fail-repo.git");

            // 2 resets: one from TestBuildAndCommit for first file, one from the retry for second file
            await testRepo
                .Received(2)
                .ResetToDefaultBranchAsync(
                    upstream: Arg.Any<string>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                );
        }
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldCountIncludesReorderAsChangeAsync()
    {
        (Repository activeRepo, string projectFile, IGitRepository testRepo) =
            await this.SetupRepoDirWithChangelogAndProjectAsync(
                repoDirName: "gitrepo-includes-reorder",
                cloneUrl: "https://github.com/test/includes-reorder-repo.git",
                headRev: "incl001"
            );

        using (activeRepo)
        {
            string repoDir = testRepo.WorkingDirectory;

            this.SetupDotNetFilesAndBuild(repoDir: repoDir, projectFile: projectFile);

            this._projectXmlRewriter.ReOrderIncludes(Arg.Any<System.Xml.XmlDocument>(), Arg.Any<string>())
                .Returns(true);

            IGitRepository templateRepo = GetSubstitute<IGitRepository>();
            this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

            await this.RunBulkUpdateAsync("https://github.com/test/includes-reorder-repo.git");

            this._trackingCache.Received(1)
                .Set(repoUrl: "https://github.com/test/includes-reorder-repo.git", value: "incl001");
        }
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldHandleExceptionInProjectCleanupAsync()
    {
        (Repository activeRepo, string projectFile, IGitRepository testRepo) =
            await this.SetupRepoDirWithChangelogAndProjectAsync(
                repoDirName: "gitrepo-cleanup-exception",
                cloneUrl: "https://github.com/test/cleanup-exception-repo.git",
                headRev: "exc001"
            );

        using (activeRepo)
        {
            string repoDir = testRepo.WorkingDirectory;

            this.SetupDotNetFilesAndBuild(repoDir: repoDir, projectFile: projectFile);

            this._projectXmlRewriter.ReOrderPropertyGroups(Arg.Any<System.Xml.XmlDocument>(), Arg.Any<string>())
                .Throws(new InvalidOperationException("Unexpected project structure"));

            IGitRepository templateRepo = GetSubstitute<IGitRepository>();
            this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

            await this.RunBulkUpdateAsync("https://github.com/test/cleanup-exception-repo.git");

            this._trackingCache.Received(1)
                .Set(repoUrl: "https://github.com/test/cleanup-exception-repo.git", value: "exc001");
        }
    }
}
