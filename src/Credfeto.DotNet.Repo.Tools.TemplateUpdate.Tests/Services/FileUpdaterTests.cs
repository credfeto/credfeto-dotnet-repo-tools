using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

public sealed class FileUpdaterTests : LoggingTestBase, IDisposable
{
    private readonly string _tempFolder;
    private readonly IFileUpdater _fileUpdater;
    private readonly IGitRepository _repository;
    private readonly RepoContext _repoContext;

    public FileUpdaterTests(ITestOutputHelper output)
        : base(output)
    {
        this._tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this._tempFolder);

        this._repository = GetSubstitute<IGitRepository>();
        this._repository.ClonePath.Returns("git@github.com:test/test.git");
        this._repository.WorkingDirectory.Returns(this._tempFolder);
        this._repository.GetDefaultBranch(GitConstants.Upstream).Returns("main");

        this._repoContext = new RepoContext(Repository: this._repository, ChangeLogFileName: "CHANGELOG.md");
        this._fileUpdater = new FileUpdater(this.GetTypedLogger<FileUpdater>());
    }

    public void Dispose()
    {
        this._repository.Dispose();

        if (Directory.Exists(this._tempFolder))
        {
            Directory.Delete(path: this._tempFolder, recursive: true);
        }
    }

    [Fact]
    public async Task SourceMissingReturnsFalse()
    {
        string sourceFile = Path.Combine(this._tempFolder, "nonexistent-source.txt");
        string targetFile = Path.Combine(this._tempFolder, "target.txt");

        CopyInstruction copyInstruction = new(
            SourceFileName: sourceFile,
            TargetFileName: targetFile,
            Apply: static bytes => (bytes, false),
            IsTargetNewer: static (_, _) => false,
            Message: "test commit"
        );

        bool result = await this._fileUpdater.UpdateFileAsync(
            repoContext: this._repoContext,
            copyInstruction: copyInstruction,
            changelogUpdate: static _ => ValueTask.CompletedTask,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(result, userMessage: "Source missing should return false");
        await this._repository.DidNotReceive().CommitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TargetMissingCopiesAndCommitsAndReturnsTrue()
    {
        string sourceFile = Path.Combine(this._tempFolder, "source.txt");
        string targetFile = Path.Combine(this._tempFolder, "target-new.txt");

        const string content = "source content";
        await File.WriteAllTextAsync(path: sourceFile, contents: content, cancellationToken: this.CancellationToken());

        CopyInstruction copyInstruction = new(
            SourceFileName: sourceFile,
            TargetFileName: targetFile,
            Apply: static bytes => (bytes, false),
            IsTargetNewer: static (_, _) => false,
            Message: "test commit"
        );

        bool result = await this._fileUpdater.UpdateFileAsync(
            repoContext: this._repoContext,
            copyInstruction: copyInstruction,
            changelogUpdate: static _ => ValueTask.CompletedTask,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(result, userMessage: "Target missing should copy file and return true");
        Assert.True(File.Exists(targetFile), userMessage: "Target file should have been created");
        await this
            ._repository.Received(1)
            .CommitAsync(message: "test commit", cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SameContentReturnsFalse()
    {
        string sourceFile = Path.Combine(this._tempFolder, "source.txt");
        string targetFile = Path.Combine(this._tempFolder, "target.txt");

        const string content = "same content";
        await File.WriteAllTextAsync(path: sourceFile, contents: content, cancellationToken: this.CancellationToken());
        await File.WriteAllTextAsync(path: targetFile, contents: content, cancellationToken: this.CancellationToken());

        CopyInstruction copyInstruction = new(
            SourceFileName: sourceFile,
            TargetFileName: targetFile,
            Apply: static bytes => (bytes, false),
            IsTargetNewer: static (_, _) => false,
            Message: "test commit"
        );

        bool result = await this._fileUpdater.UpdateFileAsync(
            repoContext: this._repoContext,
            copyInstruction: copyInstruction,
            changelogUpdate: static _ => ValueTask.CompletedTask,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(result, userMessage: "Same content should return false");
        await this._repository.DidNotReceive().CommitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DifferentContentCopiesAndCommitsAndReturnsTrue()
    {
        string sourceFile = Path.Combine(this._tempFolder, "source.txt");
        string targetFile = Path.Combine(this._tempFolder, "target.txt");

        await File.WriteAllTextAsync(
            path: sourceFile,
            contents: "new content",
            cancellationToken: this.CancellationToken()
        );
        await File.WriteAllTextAsync(
            path: targetFile,
            contents: "old content",
            cancellationToken: this.CancellationToken()
        );

        CopyInstruction copyInstruction = new(
            SourceFileName: sourceFile,
            TargetFileName: targetFile,
            Apply: static bytes => (bytes, false),
            IsTargetNewer: static (_, _) => false,
            Message: "test commit"
        );

        bool result = await this._fileUpdater.UpdateFileAsync(
            repoContext: this._repoContext,
            copyInstruction: copyInstruction,
            changelogUpdate: static _ => ValueTask.CompletedTask,
            cancellationToken: this.CancellationToken()
        );

        Assert.True(result, userMessage: "Different content should copy and return true");
        await this
            ._repository.Received(1)
            .CommitAsync(message: "test commit", cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TargetNewerReturnsFalse()
    {
        string sourceFile = Path.Combine(this._tempFolder, "source.txt");
        string targetFile = Path.Combine(this._tempFolder, "target.txt");

        await File.WriteAllTextAsync(
            path: sourceFile,
            contents: "source content",
            cancellationToken: this.CancellationToken()
        );
        await File.WriteAllTextAsync(
            path: targetFile,
            contents: "different content",
            cancellationToken: this.CancellationToken()
        );

        CopyInstruction copyInstruction = new(
            SourceFileName: sourceFile,
            TargetFileName: targetFile,
            Apply: static bytes => (bytes, false),
            IsTargetNewer: static (_, _) => true,
            Message: "test commit"
        );

        bool result = await this._fileUpdater.UpdateFileAsync(
            repoContext: this._repoContext,
            copyInstruction: copyInstruction,
            changelogUpdate: static _ => ValueTask.CompletedTask,
            cancellationToken: this.CancellationToken()
        );

        Assert.False(result, userMessage: "Target newer than source should return false");
        await this._repository.DidNotReceive().CommitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
