using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Services;

public sealed class DependencyReducerCheckReferencesTests : LoggingFolderCleanupTestBase
{
    private static readonly BuildSettings EmptyBuildSettings = new(
        PublishableProjects: [],
        PackableProjects: [],
        Framework: null
    );

    private const string MINIMAL_SDK_PROJECT_XML =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";

    private const string MINIMAL_SDK_WEB_PROJECT_XML =
        "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";

    private const string MINIMAL_SDK_RAZOR_PROJECT_XML =
        "<Project Sdk=\"Microsoft.NET.Sdk.Razor\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";

    private const string MINIMAL_SDK_WITH_SOME_PACKAGE_XML =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"SomePackage\" Version=\"1.0.0\" /></ItemGroup></Project>";

    private const string MINIMAL_SDK_WITH_FUN_FAIR_SOME_THING_XML =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"FunFair.SomeThing\" Version=\"1.0.0\" /></ItemGroup></Project>";

    private readonly IDotNetBuild _dotNetBuild;
    private readonly DependencyReducer _sut;

    public DependencyReducerCheckReferencesTests(ITestOutputHelper output)
        : base(output)
    {
        this._dotNetBuild = GetSubstitute<IDotNetBuild>();
        MockIDotNetBuildLoadBuildSettings(this._dotNetBuild, EmptyBuildSettings);

        ILogger<DependencyReducer> logger = this.GetTypedLogger<DependencyReducer>();
        this._sut = new DependencyReducer(dotNetBuild: this._dotNetBuild, logger: logger);
    }

    private static void MockIDotNetBuildLoadBuildSettings(IDotNetBuild dotNetBuild, in BuildSettings value)
    {
        dotNetBuild
            .LoadBuildSettingsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(value);
    }

    private static void MockIDotNetBuildBuild(
        IDotNetBuild dotNetBuild,
        int nthToThrow,
        string message,
        bool fromNthOnwards = false
    )
    {
        int callCount = 0;
        dotNetBuild
            .When(async b =>
                await b.BuildAsync(Arg.Any<string>(), Arg.Any<BuildContext>(), Arg.Any<CancellationToken>())
            )
            .Do(_ =>
            {
                ++callCount;

                if (fromNthOnwards ? callCount >= nthToThrow : callCount == nthToThrow)
                {
                    throw new DotNetBuildErrorException(message);
                }
            });
    }

    private static ValueTask NoOpRemovalAsync(
        string projectFileName,
        string message,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.CompletedTask;
    }

    private static ReferenceConfig BuildConfig()
    {
        return new ReferenceConfig(onSuccessfulRemoval: NoOpRemovalAsync);
    }

    private async ValueTask<string> WriteProjectFileAsync(string projectXml, string? fileName = null)
    {
        string projectFile = Path.Combine(path1: this.TempFolder, path2: fileName ?? "TestProject.csproj");
        await File.WriteAllTextAsync(
            path: projectFile,
            contents: projectXml,
            cancellationToken: this.CancellationToken()
        );

        return projectFile;
    }

    private DotNetFiles BuildDotNetFiles(params string[] projectFiles)
    {
        return new DotNetFiles(
            SourceDirectory: this.TempFolder,
            Solutions: [projectFiles[0]],
            Projects: [.. projectFiles]
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithNoProjectsShouldReturnFalseAsync()
    {
        DotNetFiles dotNetFiles = new(SourceDirectory: this.TempFolder, Solutions: [], Projects: []);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result, userMessage: "No projects should produce no changes");
        await this
            ._dotNetBuild.DidNotReceive()
            .BuildAsync(Arg.Any<string>(), Arg.Any<BuildContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithMetaProjectShouldSkipItAsync()
    {
        string projectFile = await this.WriteProjectFileAsync(
            projectXml: MINIMAL_SDK_PROJECT_XML,
            fileName: "MyMeta.All.csproj"
        );

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result, userMessage: "Meta project should be skipped");
        await this
            ._dotNetBuild.DidNotReceive()
            .BuildAsync(Arg.Any<string>(), Arg.Any<BuildContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithSimpleSdkProjectShouldReturnFalseWhenNoChangesAsync()
    {
        const string projectXml = MINIMAL_SDK_PROJECT_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(
            condition: result,
            userMessage: "Simple SDK project with no removable refs should produce no changes"
        );
        await this
            ._dotNetBuild.Received(2)
            .BuildAsync(Arg.Any<string>(), Arg.Any<BuildContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncShouldThrowWhenInitialBuildFailsAsync()
    {
        const string projectXml = MINIMAL_SDK_PROJECT_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        this._dotNetBuild.When(async b =>
                await b.BuildAsync(Arg.Any<string>(), Arg.Any<BuildContext>(), Arg.Any<CancellationToken>())
            )
            .Do(_ => throw new DotNetBuildErrorException("Build failed"));

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        await Assert.ThrowsAsync<DotNetBuildErrorException>(async () =>
            await this._sut.CheckReferencesAsync(
                dotNetFiles: dotNetFiles,
                config: config,
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithSdkWebProjectShouldNarrowSdkWhenBothBuildsSucceedAsync()
    {
        const string projectXml = MINIMAL_SDK_WEB_PROJECT_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result, userMessage: "SDK.Web with successful build should narrow to SDK");
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithSdkWebAndOutputTypeExeShouldNotNarrowSdkAsync()
    {
        const string projectXml =
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup></Project>";
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result, userMessage: "SDK.Web Exe project should not be narrowed");
        await this
            ._dotNetBuild.Received(2)
            .BuildAsync(Arg.Any<string>(), Arg.Any<BuildContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithSdkRazorAndNoRazorFilesShouldNarrowSdkAsync()
    {
        const string projectXml = MINIMAL_SDK_RAZOR_PROJECT_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result, userMessage: "SDK.Razor project with no .cshtml files should narrow SDK");
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithSdkRazorAndRazorFilesShouldNotNarrowSdkAsync()
    {
        const string projectXml = MINIMAL_SDK_RAZOR_PROJECT_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        string cshtmlFile = Path.Combine(path1: this.TempFolder, path2: "Index.cshtml");
        await File.WriteAllTextAsync(
            path: cshtmlFile,
            contents: "<html></html>",
            cancellationToken: this.CancellationToken()
        );

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result, userMessage: "SDK.Razor project with .cshtml files should not narrow SDK");
        await this
            ._dotNetBuild.Received(2)
            .BuildAsync(Arg.Any<string>(), Arg.Any<BuildContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncShouldRestoreSdkWhenMinimalSdkProjectBuildFailsAsync()
    {
        const string projectXml = MINIMAL_SDK_WEB_PROJECT_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        MockIDotNetBuildBuild(dotNetBuild: this._dotNetBuild, nthToThrow: 2, message: "Build failed with minimal SDK");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result, userMessage: "SDK narrowing failure should restore and report no changes");
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithRemovablePackageShouldReturnTrueAndCallOnSuccessfulRemovalAsync()
    {
        const string projectXml = MINIMAL_SDK_WITH_SOME_PACKAGE_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);

        string? removedProject = null;
        string? removedMessage = null;

        ReferenceConfig config = new(
            onSuccessfulRemoval: (project, message, _) =>
            {
                removedProject = project;
                removedMessage = message;

                return ValueTask.CompletedTask;
            }
        );

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result, userMessage: "Removable package should result in a change");
        Assert.Equal(expected: projectFile, actual: removedProject);
        Assert.NotNull(removedMessage);
        Assert.Contains(
            expectedSubstring: "SomePackage",
            actualString: removedMessage,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithDoNotRemovePackageShouldSkipAndReturnFalseAsync()
    {
        const string projectXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"xunit\" Version=\"2.9.0\" /></ItemGroup></Project>";
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result, userMessage: "Do-not-remove package should be skipped");
        await this
            ._dotNetBuild.Received(2)
            .BuildAsync(Arg.Any<string>(), Arg.Any<BuildContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithRemovableProjectReferenceShouldReturnTrueAndCallOnSuccessfulRemovalAsync()
    {
        string childDir = Path.Combine(path1: this.TempFolder, path2: "ChildProject");
        Directory.CreateDirectory(childDir);

        const string childXml = MINIMAL_SDK_PROJECT_XML;
        string childFile = Path.Combine(path1: childDir, path2: "ChildProject.csproj");
        await File.WriteAllTextAsync(path: childFile, contents: childXml, cancellationToken: this.CancellationToken());

        const string parentXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"ChildProject/ChildProject.csproj\" /></ItemGroup></Project>";
        string parentFile = await this.WriteProjectFileAsync(parentXml, fileName: "ParentProject.csproj");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(parentFile);

        string? removedProject = null;
        string? removedMessage = null;

        ReferenceConfig config = new(
            onSuccessfulRemoval: (project, message, _) =>
            {
                removedProject = project;
                removedMessage = message;

                return ValueTask.CompletedTask;
            }
        );

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result, userMessage: "Removable project reference should result in a change");
        Assert.Equal(expected: parentFile, actual: removedProject);
        Assert.NotNull(removedMessage);
        Assert.Contains(
            expectedSubstring: "ChildProject",
            actualString: removedMessage,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithPackageAlsoInChildProjectShouldRemoveWithoutSeparateBuildAsync()
    {
        string childDir = Path.Combine(path1: this.TempFolder, path2: "ChildWithPkg");
        Directory.CreateDirectory(childDir);

        const string childXml = MINIMAL_SDK_WITH_SOME_PACKAGE_XML;
        string childFile = Path.Combine(path1: childDir, path2: "ChildWithPkg.csproj");
        await File.WriteAllTextAsync(path: childFile, contents: childXml, cancellationToken: this.CancellationToken());

        const string parentXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"SomePackage\" Version=\"1.0.0\" /><ProjectReference Include=\"ChildWithPkg/ChildWithPkg.csproj\" /></ItemGroup></Project>";
        string parentFile = await this.WriteProjectFileAsync(parentXml, fileName: "ParentWithPkg.csproj");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(parentFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result, userMessage: "Package covered by child project should be removable");
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncShouldThrowWhenFinalBuildFailsAsync()
    {
        const string projectXml = MINIMAL_SDK_PROJECT_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        MockIDotNetBuildBuild(
            dotNetBuild: this._dotNetBuild,
            nthToThrow: 2,
            message: "Final build failed",
            fromNthOnwards: true
        );

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        await Assert.ThrowsAsync<DotNetBuildErrorException>(async () =>
            await this._sut.CheckReferencesAsync(
                dotNetFiles: dotNetFiles,
                config: config,
                cancellationToken: this.CancellationToken()
            )
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncShouldNotCallOnSuccessfulRemovalWhenSolutionBuildFailsAfterPackageRemovalAsync()
    {
        const string projectXml = MINIMAL_SDK_WITH_SOME_PACKAGE_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        this._dotNetBuild.When(async b => await b.BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new DotNetBuildErrorException("Solution build failed after package removal"));

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);

        bool onSuccessfulRemovalCalled = false;

        ReferenceConfig config = new(
            onSuccessfulRemoval: (_, _, _) =>
            {
                onSuccessfulRemovalCalled = true;

                return ValueTask.CompletedTask;
            }
        );

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result, userMessage: "Package removal should be tracked even when solution build fails");
        Assert.False(
            condition: onSuccessfulRemovalCalled,
            userMessage: "OnSuccessfulRemoval should not be called when solution build fails"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithMultipleProjectsShouldReturnTrueWhenAnyHasChangesAsync()
    {
        const string projectXml1 = MINIMAL_SDK_WEB_PROJECT_XML;
        const string projectXml2 = MINIMAL_SDK_WITH_SOME_PACKAGE_XML;

        string projectFile1 = await this.WriteProjectFileAsync(projectXml1, fileName: "Project1.csproj");

        string project2Dir = Path.Combine(path1: this.TempFolder, path2: "Project2");
        Directory.CreateDirectory(project2Dir);
        string projectFile2 = Path.Combine(path1: project2Dir, path2: "Project2.csproj");
        await File.WriteAllTextAsync(
            path: projectFile2,
            contents: projectXml2,
            cancellationToken: this.CancellationToken()
        );

        DotNetFiles dotNetFiles = new(
            SourceDirectory: this.TempFolder,
            Solutions: [projectFile1],
            Projects: [projectFile1, projectFile2]
        );
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result, userMessage: "Multiple projects with changes should return true");
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithProjectWithoutSdkAttributeShouldReturnFalseAsync()
    {
        const string projectXml =
            "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(condition: result, userMessage: "Project without Sdk attribute should produce no changes");
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithNonFunFairPackageRemovalBuildFailureShouldReturnFalseAsync()
    {
        const string projectXml = MINIMAL_SDK_WITH_SOME_PACKAGE_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        MockIDotNetBuildBuild(dotNetBuild: this._dotNetBuild, nthToThrow: 2, message: "Package removal build failed");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(
            condition: result,
            userMessage: "Non-FunFair package with removal build failure should report no narrowing"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithFunFairPackageRemovalBuildFailureAndNoSourceFilesShouldTrackNarrowingAsync()
    {
        const string projectXml = MINIMAL_SDK_WITH_FUN_FAIR_SOME_THING_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        MockIDotNetBuildBuild(dotNetBuild: this._dotNetBuild, nthToThrow: 2, message: "Package removal build failed");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(
            condition: result,
            userMessage: "FunFair package with no source files should be tracked for narrowing"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithFunFairPackageRemovalBuildFailureAndUsingInSourceFileShouldReturnFalseAsync()
    {
        const string projectXml = MINIMAL_SDK_WITH_FUN_FAIR_SOME_THING_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        string sourceFile = Path.Combine(path1: this.TempFolder, path2: "Program.cs");
        await File.WriteAllTextAsync(
            path: sourceFile,
            contents: "using FunFair.SomeThing;\nclass Program { }",
            cancellationToken: this.CancellationToken()
        );

        MockIDotNetBuildBuild(dotNetBuild: this._dotNetBuild, nthToThrow: 2, message: "Package removal build failed");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(
            condition: result,
            userMessage: "FunFair package referenced in source via using should not be tracked for narrowing"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithFunFairPackageRemovalBuildFailureAndNamespaceInSourceFileShouldReturnFalseAsync()
    {
        const string projectXml = MINIMAL_SDK_WITH_FUN_FAIR_SOME_THING_XML;
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        string sourceFile = Path.Combine(path1: this.TempFolder, path2: "MyClass.cs");
        await File.WriteAllTextAsync(
            path: sourceFile,
            contents: "namespace FunFair.SomeThing.SubNamespace;\nclass MyClass { }",
            cancellationToken: this.CancellationToken()
        );

        MockIDotNetBuildBuild(dotNetBuild: this._dotNetBuild, nthToThrow: 2, message: "Package removal build failed");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(
            condition: result,
            userMessage: "FunFair package referenced in source via namespace should not be tracked for narrowing"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithFunFairAllPackageRemovalBuildFailureShouldReturnFalseAsync()
    {
        const string projectXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"FunFair.Something.All\" Version=\"1.0.0\" /></ItemGroup></Project>";
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        MockIDotNetBuildBuild(dotNetBuild: this._dotNetBuild, nthToThrow: 2, message: "Package removal build failed");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(
            condition: result,
            userMessage: "FunFair grouping package (.All) should not be tracked for narrowing"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithProjectReferenceRemovalBuildFailureShouldRestoreAsync()
    {
        string childDir = Path.Combine(path1: this.TempFolder, path2: "ChildProjFail");
        Directory.CreateDirectory(childDir);

        const string childXml = MINIMAL_SDK_PROJECT_XML;
        string childFile = Path.Combine(path1: childDir, path2: "ChildProjFail.csproj");
        await File.WriteAllTextAsync(path: childFile, contents: childXml, cancellationToken: this.CancellationToken());

        const string parentXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"ChildProjFail/ChildProjFail.csproj\" /></ItemGroup></Project>";
        string parentFile = await this.WriteProjectFileAsync(parentXml, fileName: "ParentProjFail.csproj");

        MockIDotNetBuildBuild(
            dotNetBuild: this._dotNetBuild,
            nthToThrow: 2,
            message: "Project ref removal build failed"
        );

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(parentFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(
            condition: result,
            userMessage: "Project ref removal build failure should restore and report no changes"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithProjectAlsoInGrandchildShouldSkipBuildForCoveredReferenceAsync()
    {
        const string grandChildXml = MINIMAL_SDK_PROJECT_XML;
        string grandChildFile = Path.Combine(path1: this.TempFolder, path2: "GrandChild.csproj");
        await File.WriteAllTextAsync(
            path: grandChildFile,
            contents: grandChildXml,
            cancellationToken: this.CancellationToken()
        );

        const string childXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"GrandChild.csproj\" /></ItemGroup></Project>";
        string childFile = Path.Combine(path1: this.TempFolder, path2: "Child.csproj");
        await File.WriteAllTextAsync(path: childFile, contents: childXml, cancellationToken: this.CancellationToken());

        const string parentXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"Child.csproj\" /><ProjectReference Include=\"GrandChild.csproj\" /></ItemGroup></Project>";
        string parentFile = await this.WriteProjectFileAsync(parentXml, fileName: "ParentThreeLevels.csproj");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(parentFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(condition: result, userMessage: "Parent with grandchild-covered project ref should report changes");
        await this
            ._dotNetBuild.Received(3)
            .BuildAsync(parentFile, Arg.Any<BuildContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithProjectAlreadyIncludedByChildInSameFolderShouldSkipBuildAndReportChangeAsync()
    {
        const string bXml = MINIMAL_SDK_PROJECT_XML;
        const string aXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"B.csproj\" /></ItemGroup></Project>";
        const string parentXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"A.csproj\" /><ProjectReference Include=\"B.csproj\" /></ItemGroup></Project>";

        string bFile = Path.Combine(path1: this.TempFolder, path2: "B.csproj");
        string aFile = Path.Combine(path1: this.TempFolder, path2: "A.csproj");
        string parentFile = await this.WriteProjectFileAsync(parentXml, fileName: "Parent.csproj");

        await File.WriteAllTextAsync(path: bFile, contents: bXml, cancellationToken: this.CancellationToken());
        await File.WriteAllTextAsync(path: aFile, contents: aXml, cancellationToken: this.CancellationToken());

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(parentFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(
            condition: result,
            userMessage: "Parent with B.csproj also included by A.csproj should report changes without building for B"
        );
        await this
            ._dotNetBuild.Received(3)
            .BuildAsync(Arg.Any<string>(), Arg.Any<BuildContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithFunFairProjectReferenceRemovalBuildFailureAndNoSourceFilesShouldTrackNarrowingAsync()
    {
        const string funFairXml = MINIMAL_SDK_PROJECT_XML;
        const string parentXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"FunFair.SomeThing.csproj\" /></ItemGroup></Project>";

        string funFairFile = Path.Combine(path1: this.TempFolder, path2: "FunFair.SomeThing.csproj");
        string parentFile = await this.WriteProjectFileAsync(parentXml, fileName: "Parent.csproj");

        await File.WriteAllTextAsync(
            path: funFairFile,
            contents: funFairXml,
            cancellationToken: this.CancellationToken()
        );

        MockIDotNetBuildBuild(
            dotNetBuild: this._dotNetBuild,
            nthToThrow: 2,
            message: "Project ref removal build failed"
        );

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(parentFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(
            condition: result,
            userMessage: "FunFair project reference with no source usage should be tracked for narrowing"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithNonCsprojProjectReferenceRemovalBuildFailureShouldNotTrackNarrowingAsync()
    {
        const string propsXml = "<Project />";
        const string parentXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"SomeFile.props\" /></ItemGroup></Project>";

        string propsFile = Path.Combine(path1: this.TempFolder, path2: "SomeFile.props");
        string parentFile = await this.WriteProjectFileAsync(parentXml, fileName: "ParentWithProps.csproj");

        await File.WriteAllTextAsync(path: propsFile, contents: propsXml, cancellationToken: this.CancellationToken());

        MockIDotNetBuildBuild(
            dotNetBuild: this._dotNetBuild,
            nthToThrow: 2,
            message: "Project ref removal build failed"
        );

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(parentFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(
            condition: result,
            userMessage: "Non-csproj project reference with failed removal build should not be tracked for narrowing"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithChildProjectHavingDifferentPackageShouldBuildAndRemoveAsync()
    {
        string childDir = Path.Combine(path1: this.TempFolder, path2: "ChildDiffPkg");
        Directory.CreateDirectory(childDir);

        const string childXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"DifferentPackage\" Version=\"1.0.0\" /></ItemGroup></Project>";
        string childFile = Path.Combine(path1: childDir, path2: "ChildDiffPkg.csproj");
        await File.WriteAllTextAsync(path: childFile, contents: childXml, cancellationToken: this.CancellationToken());

        const string parentXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"SomePackage\" Version=\"1.0.0\" /><ProjectReference Include=\"ChildDiffPkg/ChildDiffPkg.csproj\" /></ItemGroup></Project>";
        string parentFile = await this.WriteProjectFileAsync(parentXml, fileName: "ParentDiffPkg.csproj");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(parentFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(
            condition: result,
            userMessage: "Parent package not present in child should still be removable when build succeeds"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithProjectReferenceWithEmptyIncludeShouldBeIgnoredAsync()
    {
        const string projectXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"\" /></ItemGroup></Project>";
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(
            condition: result,
            userMessage: "Empty-Include project reference should be filtered out and produce no changes"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithWindowsStyleProjectReferencePathShouldNormalizeAndSucceedAsync()
    {
        string childDir = Path.Combine(path1: this.TempFolder, path2: "WinChild");
        Directory.CreateDirectory(childDir);

        const string childXml = MINIMAL_SDK_PROJECT_XML;
        string childFile = Path.Combine(path1: childDir, path2: "WinChild.csproj");
        await File.WriteAllTextAsync(path: childFile, contents: childXml, cancellationToken: this.CancellationToken());

        const string parentXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"WinChild\\WinChild.csproj\" /></ItemGroup></Project>";
        string parentFile = await this.WriteProjectFileAsync(parentXml, fileName: "ParentWinPath.csproj");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(parentFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(
            condition: result,
            userMessage: "Windows-style backslash project reference path should be normalised on Linux and reference removed"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithChildProjectHavingEmptyIncludePackageReferenceShouldSkipPackageAsync()
    {
        string childDir = Path.Combine(path1: this.TempFolder, path2: "ChildEmptyPkg");
        Directory.CreateDirectory(childDir);

        const string childXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"\" Version=\"1.0.0\" /></ItemGroup></Project>";
        string childFile = Path.Combine(path1: childDir, path2: "ChildEmptyPkg.csproj");
        await File.WriteAllTextAsync(path: childFile, contents: childXml, cancellationToken: this.CancellationToken());

        const string parentXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"ChildEmptyPkg/ChildEmptyPkg.csproj\" /></ItemGroup></Project>";
        string parentFile = await this.WriteProjectFileAsync(parentXml, fileName: "ParentEmptyPkg.csproj");

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(parentFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(
            condition: result,
            userMessage: "Child project with empty-Include PackageReference should have it filtered out; parent project reference should still be removable"
        );
    }

    [Fact]
    public async ValueTask CheckReferencesAsyncWithNonProjectRootElementShouldSkipSdkCheckAndReturnFalseAsync()
    {
        const string projectXml =
            "<Root Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Root>";
        string projectFile = await this.WriteProjectFileAsync(projectXml);

        DotNetFiles dotNetFiles = this.BuildDotNetFiles(projectFile);
        ReferenceConfig config = BuildConfig();

        bool result = await this._sut.CheckReferencesAsync(
            dotNetFiles: dotNetFiles,
            config: config,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(
            condition: result,
            userMessage: "Project file with non-<Project> root element should skip SDK check and produce no changes"
        );
    }
}
