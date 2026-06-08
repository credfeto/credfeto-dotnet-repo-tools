using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Packages.Services;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Credfeto.Package;
using FunFair.Test.Common;
using NSubstitute;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Packages.Tests.Services;

public sealed class SinglePackageUpdaterTests : LoggingFolderCleanupTestBase
{
    private const string CLONE_PATH = "git@github.com:test/test-repo.git";
    private const string DEFAULT_BRANCH = "main";
    private const string HEAD_REV = "abc123def456";

    private static readonly DotNetVersionSettings DefaultDotNetSettings = new(
        SdkVersion: null,
        AllowPreRelease: false,
        RollForward: "latestMajor"
    );

    private static readonly BuildSettings EmptyBuildSettings = new(
        PublishableProjects: [],
        PackableProjects: [],
        Framework: null
    );

    private readonly IDotNetBuild _dotNetBuild;
    private readonly IDotNetSolutionCheck _dotNetSolutionCheck;
    private readonly IPackageUpdateConfigurationBuilder _packageUpdateConfigurationBuilder;
    private readonly IPackageUpdater _packageUpdater;
    private readonly IGitRepository _repository;
    private readonly ISinglePackageUpdater _singlePackageUpdater;
    private readonly ITrackingCache _trackingCache;
    private readonly ITrackingHashGenerator _trackingHashGenerator;

    public SinglePackageUpdaterTests(ITestOutputHelper output)
        : base(output)
    {
        this._dotNetSolutionCheck = GetSubstitute<IDotNetSolutionCheck>();
        this._dotNetBuild = GetSubstitute<IDotNetBuild>();
        this._trackingCache = GetSubstitute<ITrackingCache>();
        this._trackingHashGenerator = GetSubstitute<ITrackingHashGenerator>();
        this._packageUpdater = GetSubstitute<IPackageUpdater>();
        this._packageUpdateConfigurationBuilder = GetSubstitute<IPackageUpdateConfigurationBuilder>();

        this._repository = GetSubstitute<IGitRepository>();
        this._repository.ClonePath.Returns(CLONE_PATH);
        this._repository.WorkingDirectory.Returns(this.TempFolder);
        this._repository.GetDefaultBranch(GitConstants.Upstream).Returns(DEFAULT_BRANCH);
        this._repository.HeadRev.Returns(HEAD_REV);

        this._singlePackageUpdater = new SinglePackageUpdater(
            dotNetSolutionCheck: this._dotNetSolutionCheck,
            dotNetBuild: this._dotNetBuild,
            trackingCache: this._trackingCache,
            trackingHashGenerator: this._trackingHashGenerator,
            packageUpdater: this._packageUpdater,
            packageUpdateConfigurationBuilder: this._packageUpdateConfigurationBuilder,
            logger: this.GetTypedLogger<SinglePackageUpdater>()
        );
    }

    private async Task<string> CreateChangelogFileAsync()
    {
        string changelogPath = Path.Combine(path1: this.TempFolder, path2: "CHANGELOG.md");
        const string content = """
            # Changelog
            All notable changes to this project will be documented in this file.

            ## [Unreleased]
            ### Changed

            ## [0.0.1] - 2024-01-01
            ### Changed
            - Initial release
            """;
        await File.WriteAllTextAsync(
            path: changelogPath,
            contents: content,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken: this.CancellationToken()
        );

        return changelogPath;
    }

    private RepoContext CreateRepoContext(string changelogFileName)
    {
        return new RepoContext(
            ClonePath: CLONE_PATH,
            Repository: this._repository,
            WorkingDirectory: this.TempFolder,
            DefaultBranch: DEFAULT_BRANCH,
            ChangeLogFileName: changelogFileName
        );
    }

    private static PackageUpdateContext CreateUpdateContext()
    {
        return new PackageUpdateContext(
            WorkFolder: "/tmp/work",
            CacheFileName: null,
            TrackingFileName: "/tmp/tracking.json",
            AdditionalSources: [],
            DotNetSettings: DefaultDotNetSettings,
            ReleaseConfig: new(
                AutoReleasePendingPackages: 1,
                MinimumHoursBeforeAutoRelease: 5,
                InactivityHoursBeforeAutoRelease: 9,
                NeverRelease: [],
                AllowedAutoUpgrade: [],
                AlwaysMatch: []
            )
        );
    }

    private static PackageUpdate CreatePackage(string packageId = "Test.Package")
    {
        return new PackageUpdate(
            packageId: packageId,
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateWithNoUncommittedChangesAndMatchingTrackingHashAndNoPackageUpdatesReturnsFalseAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync();
        RepoContext repoContext = this.CreateRepoContext(changelogPath);
        PackageUpdateContext updateContext = CreateUpdateContext();
        PackageUpdate package = CreatePackage();

        // No uncommitted changes
        this._repository.HasUncommittedChanges().Returns(false);

        // Tracking hash matches HeadRev - so no build required
        this._trackingCache.Get(CLONE_PATH).Returns(HEAD_REV);

        // No package updates
        PackageUpdateConfiguration config = new(
            PackageMatch: new PackageMatch(PackageId: "Test.Package", Prefix: true),
            ExcludedPackages: []
        );
        this._packageUpdateConfigurationBuilder.Build(package).Returns(config);
        IReadOnlyList<PackageVersion> noPackageUpdates = [];
        this._packageUpdater.UpdateAsync(
                basePath: this.TempFolder,
                configuration: config,
                packageSources: updateContext.AdditionalSources,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(noPackageUpdates);

        bool result = await this._singlePackageUpdater.UpdateAsync(
            updateContext: updateContext,
            repoContext: repoContext,
            solutions: [],
            sourceDirectory: this.TempFolder,
            buildSettings: EmptyBuildSettings,
            dotNetSettings: DefaultDotNetSettings,
            package: package,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(result, userMessage: "Update should return false when no packages were updated");
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateWithNoPackageUpdatesRemovesBranchesForPackageAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync();
        RepoContext repoContext = this.CreateRepoContext(changelogPath);
        PackageUpdateContext updateContext = CreateUpdateContext();
        PackageUpdate package = CreatePackage();

        this._repository.HasUncommittedChanges().Returns(false);
        this._trackingCache.Get(CLONE_PATH).Returns(HEAD_REV);

        PackageUpdateConfiguration config = new(
            PackageMatch: new PackageMatch(PackageId: "Test.Package", Prefix: true),
            ExcludedPackages: []
        );
        this._packageUpdateConfigurationBuilder.Build(package).Returns(config);
        IReadOnlyList<PackageVersion> noPackageUpdates = [];
        this._packageUpdater.UpdateAsync(
                basePath: Arg.Any<string>(),
                configuration: Arg.Any<PackageUpdateConfiguration>(),
                packageSources: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(noPackageUpdates);

        await this._singlePackageUpdater.UpdateAsync(
            updateContext: updateContext,
            repoContext: repoContext,
            solutions: [],
            sourceDirectory: this.TempFolder,
            buildSettings: EmptyBuildSettings,
            dotNetSettings: DefaultDotNetSettings,
            package: package,
            cancellationToken: this.CancellationToken()
        );

        await this
            ._repository.Received(1)
            .RemoveBranchesForPrefixAsync(
                branchForUpdate: Arg.Any<string>(),
                branchPrefix: Arg.Is<string>(s => s.Contains("test.package")),
                upstream: GitConstants.Upstream,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateWithUncommittedChangesResetsToDefaultBranchBeforeProcessingAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync();
        RepoContext repoContext = this.CreateRepoContext(changelogPath);
        PackageUpdateContext updateContext = CreateUpdateContext();
        PackageUpdate package = CreatePackage();

        // Has uncommitted changes
        this._repository.HasUncommittedChanges().Returns(true);
        this._trackingCache.Get(CLONE_PATH).Returns(HEAD_REV);

        PackageUpdateConfiguration config = new(
            PackageMatch: new PackageMatch(PackageId: "Test.Package", Prefix: true),
            ExcludedPackages: []
        );
        this._packageUpdateConfigurationBuilder.Build(package).Returns(config);
        IReadOnlyList<PackageVersion> noPackageUpdates = [];
        this._packageUpdater.UpdateAsync(
                basePath: Arg.Any<string>(),
                configuration: Arg.Any<PackageUpdateConfiguration>(),
                packageSources: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(noPackageUpdates);

        await this._singlePackageUpdater.UpdateAsync(
            updateContext: updateContext,
            repoContext: repoContext,
            solutions: [],
            sourceDirectory: this.TempFolder,
            buildSettings: EmptyBuildSettings,
            dotNetSettings: DefaultDotNetSettings,
            package: package,
            cancellationToken: this.CancellationToken()
        );

        await this
            ._repository.Received(2)
            .ResetToDefaultBranchAsync(
                upstream: GitConstants.Upstream,
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateWithNullTrackingHashRunsBuildAndUpdatesHashAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync();
        RepoContext repoContext = this.CreateRepoContext(changelogPath);
        PackageUpdateContext updateContext = CreateUpdateContext();
        PackageUpdate package = CreatePackage();

        this._repository.HasUncommittedChanges().Returns(false);

        // No prior tracking hash
        this._trackingCache.Get(CLONE_PATH).Returns((string?)null);
        this._trackingHashGenerator.GenerateTrackingHashAsync(
                repoContext: Arg.Any<RepoContext>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns("newhash123");

        PackageUpdateConfiguration config = new(
            PackageMatch: new PackageMatch(PackageId: "Test.Package", Prefix: true),
            ExcludedPackages: []
        );
        this._packageUpdateConfigurationBuilder.Build(package).Returns(config);
        IReadOnlyList<PackageVersion> noPackageUpdates = [];
        this._packageUpdater.UpdateAsync(
                basePath: Arg.Any<string>(),
                configuration: Arg.Any<PackageUpdateConfiguration>(),
                packageSources: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(noPackageUpdates);

        await this._singlePackageUpdater.UpdateAsync(
            updateContext: updateContext,
            repoContext: repoContext,
            solutions: [],
            sourceDirectory: this.TempFolder,
            buildSettings: EmptyBuildSettings,
            dotNetSettings: DefaultDotNetSettings,
            package: package,
            cancellationToken: this.CancellationToken()
        );

        await this._dotNetBuild.Received(1).BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>());
        await this
            ._trackingHashGenerator.Received(1)
            .GenerateTrackingHashAsync(
                repoContext: Arg.Any<RepoContext>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateWithPackageUpdatesAndPostCheckReturnsFalseReturnsTrueAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync();
        RepoContext repoContext = this.CreateRepoContext(changelogPath);
        PackageUpdateContext updateContext = CreateUpdateContext();
        PackageUpdate package = CreatePackage();

        this._repository.HasUncommittedChanges().Returns(false);
        this._trackingCache.Get(CLONE_PATH).Returns(HEAD_REV);

        PackageUpdateConfiguration config = new(
            PackageMatch: new PackageMatch(PackageId: "Test.Package", Prefix: true),
            ExcludedPackages: []
        );
        this._packageUpdateConfigurationBuilder.Build(package).Returns(config);

        // Return one update
        PackageVersion updatedPackage = new(packageId: "Test.Package", version: new NuGetVersion(1, 2, 3));
        IReadOnlyList<PackageVersion> packageUpdates = [updatedPackage];
        this._packageUpdater.UpdateAsync(
                basePath: Arg.Any<string>(),
                configuration: Arg.Any<PackageUpdateConfiguration>(),
                packageSources: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(packageUpdates);

        // PostCheck returns false - so don't build
        this._dotNetSolutionCheck.PostCheckAsync(
                solutions: Arg.Any<IReadOnlyList<string>>(),
                repositoryDotNetSettings: Arg.Any<DotNetVersionSettings>(),
                templateDotNetSettings: Arg.Any<DotNetVersionSettings>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(false);

        // Branch doesn't exist
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(false);

        bool result = await this._singlePackageUpdater.UpdateAsync(
            updateContext: updateContext,
            repoContext: repoContext,
            solutions: [],
            sourceDirectory: this.TempFolder,
            buildSettings: EmptyBuildSettings,
            dotNetSettings: DefaultDotNetSettings,
            package: package,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(result, userMessage: "Update should return true when packages were updated");
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateWithPackageUpdatesAndPostCheckBuildErrorReturnsTrueAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync();
        RepoContext repoContext = this.CreateRepoContext(changelogPath);
        PackageUpdateContext updateContext = CreateUpdateContext();
        PackageUpdate package = CreatePackage();

        this._repository.HasUncommittedChanges().Returns(false);
        this._trackingCache.Get(CLONE_PATH).Returns(HEAD_REV);

        PackageUpdateConfiguration config = new(
            PackageMatch: new PackageMatch(PackageId: "Test.Package", Prefix: true),
            ExcludedPackages: []
        );
        this._packageUpdateConfigurationBuilder.Build(package).Returns(config);

        PackageVersion updatedPackage = new(packageId: "Test.Package", version: new NuGetVersion(1, 2, 3));
        IReadOnlyList<PackageVersion> packageUpdates = [updatedPackage];
        this._packageUpdater.UpdateAsync(
                basePath: Arg.Any<string>(),
                configuration: Arg.Any<PackageUpdateConfiguration>(),
                packageSources: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(packageUpdates);

        // PostCheck returns true, then build throws
        this._dotNetSolutionCheck.PostCheckAsync(
                solutions: Arg.Any<IReadOnlyList<string>>(),
                repositoryDotNetSettings: Arg.Any<DotNetVersionSettings>(),
                templateDotNetSettings: Arg.Any<DotNetVersionSettings>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(true);

        this._dotNetBuild.When(async x => await x.BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new DotNetBuildErrorException("Build failed after package update"));

        // Branch doesn't exist
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(false);

        bool result = await this._singlePackageUpdater.UpdateAsync(
            updateContext: updateContext,
            repoContext: repoContext,
            solutions: [],
            sourceDirectory: this.TempFolder,
            buildSettings: EmptyBuildSettings,
            dotNetSettings: DefaultDotNetSettings,
            package: package,
            cancellationToken: this.CancellationToken()
        );

        // Returns true (update was made, even though build failed - it commits to named branch)
        Assert.True(result, userMessage: "Update should return true when packages were updated even if build failed");
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateWithPackageUpdatesWhenBranchAlreadyExistsLogsSkipAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync();
        RepoContext repoContext = this.CreateRepoContext(changelogPath);
        PackageUpdateContext updateContext = CreateUpdateContext();
        PackageUpdate package = CreatePackage();

        this._repository.HasUncommittedChanges().Returns(false);
        this._trackingCache.Get(CLONE_PATH).Returns(HEAD_REV);

        PackageUpdateConfiguration config = new(
            PackageMatch: new PackageMatch(PackageId: "Test.Package", Prefix: true),
            ExcludedPackages: []
        );
        this._packageUpdateConfigurationBuilder.Build(package).Returns(config);

        PackageVersion updatedPackage = new(packageId: "Test.Package", version: new NuGetVersion(1, 2, 3));
        IReadOnlyList<PackageVersion> packageUpdates = [updatedPackage];
        this._packageUpdater.UpdateAsync(
                basePath: Arg.Any<string>(),
                configuration: Arg.Any<PackageUpdateConfiguration>(),
                packageSources: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(packageUpdates);

        // PostCheck returns false (build not ok)
        this._dotNetSolutionCheck.PostCheckAsync(
                solutions: Arg.Any<IReadOnlyList<string>>(),
                repositoryDotNetSettings: Arg.Any<DotNetVersionSettings>(),
                templateDotNetSettings: Arg.Any<DotNetVersionSettings>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(false);

        // Branch already exists
        this._repository.DoesBranchExist(Arg.Any<string>()).Returns(true);

        bool result = await this._singlePackageUpdater.UpdateAsync(
            updateContext: updateContext,
            repoContext: repoContext,
            solutions: [],
            sourceDirectory: this.TempFolder,
            buildSettings: EmptyBuildSettings,
            dotNetSettings: DefaultDotNetSettings,
            package: package,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(result, userMessage: "Update should return true when packages were updated");
        // Branch creation should not be called since it already exists
        await this._repository.DidNotReceive().CreateBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateWithPackageUpdatesAndBuildOkCommitsToDefaultBranchAsync()
    {
        string changelogPath = await this.CreateChangelogFileAsync();
        RepoContext repoContext = this.CreateRepoContext(changelogPath);
        PackageUpdateContext updateContext = CreateUpdateContext();
        PackageUpdate package = CreatePackage();

        this._repository.HasUncommittedChanges().Returns(false);
        this._trackingCache.Get(CLONE_PATH).Returns(HEAD_REV);
        this._trackingHashGenerator.GenerateTrackingHashAsync(
                repoContext: Arg.Any<RepoContext>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns("newhash");

        PackageUpdateConfiguration config = new(
            PackageMatch: new PackageMatch(PackageId: "Test.Package", Prefix: true),
            ExcludedPackages: []
        );
        this._packageUpdateConfigurationBuilder.Build(package).Returns(config);

        PackageVersion updatedPackage = new(packageId: "Test.Package", version: new NuGetVersion(1, 2, 3));
        IReadOnlyList<PackageVersion> packageUpdates = [updatedPackage];
        this._packageUpdater.UpdateAsync(
                basePath: Arg.Any<string>(),
                configuration: Arg.Any<PackageUpdateConfiguration>(),
                packageSources: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(packageUpdates);

        // PostCheck returns true, build succeeds
        this._dotNetSolutionCheck.PostCheckAsync(
                solutions: Arg.Any<IReadOnlyList<string>>(),
                repositoryDotNetSettings: Arg.Any<DotNetVersionSettings>(),
                templateDotNetSettings: Arg.Any<DotNetVersionSettings>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(true);

        bool result = await this._singlePackageUpdater.UpdateAsync(
            updateContext: updateContext,
            repoContext: repoContext,
            solutions: [],
            sourceDirectory: this.TempFolder,
            buildSettings: EmptyBuildSettings,
            dotNetSettings: DefaultDotNetSettings,
            package: package,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(result, userMessage: "Update should return true when packages were updated and build succeeded");
        await this._repository.Received(1).PushAsync(Arg.Any<CancellationToken>());
    }
}
