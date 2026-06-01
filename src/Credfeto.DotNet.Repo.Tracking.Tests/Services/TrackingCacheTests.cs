using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Credfeto.DotNet.Repo.Tracking.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tracking.Tests.Services;

public sealed class TrackingCacheTests : LoggingFolderCleanupTestBase
{
    private readonly ITrackingCache _trackingCache;

    public TrackingCacheTests(ITestOutputHelper output)
        : base(output)
    {
        this._trackingCache = new TrackingCache(this.GetTypedLogger<TrackingCache>());

        if (!Directory.Exists(this.TempFolder))
        {
            Directory.CreateDirectory(this.TempFolder);
        }
    }

    private static string NewId => Guid.NewGuid().ToString();

    [Fact]
    public void GetNonExistent()
    {
        string? value = this._trackingCache.Get(Guid.NewGuid().ToString());
        Assert.Null(value);
    }

    [Fact]
    public void SetAndGet()
    {
        string repoUrl = Guid.NewGuid().ToString();

        string? value = this._trackingCache.Get(repoUrl);
        Assert.Null(value);

        string expected = Guid.NewGuid().ToString();

        this._trackingCache.Set(repoUrl: repoUrl, value: expected);

        value = this._trackingCache.Get(repoUrl);
        Assert.Equal(expected: expected, actual: value);
    }

    [Fact]
    public Task LoadNonExistentFileAsync()
    {
        string trackingFile = Path.Combine(path1: this.TempFolder, $"{NewId}.json");

        return Assert.ThrowsAsync<FileNotFoundException>(() =>
            this._trackingCache.LoadAsync(fileName: trackingFile, this.CancellationToken()).AsTask()
        );
    }

    [Fact]
    public async Task LoadEmptyFileAsync()
    {
        string trackingFile = Path.Combine(path1: this.TempFolder, $"{NewId}.json");
        this.Output.WriteLine(trackingFile);

        await File.WriteAllTextAsync(path: trackingFile, contents: "{}", cancellationToken: this.CancellationToken());

        await this._trackingCache.LoadAsync(fileName: trackingFile, cancellationToken: this.CancellationToken());

        string? result = this._trackingCache.Get("Test1");
        Assert.Null(result);
        string? result2 = this._trackingCache.Get("Test2");
        Assert.Null(result2);
        string? result3 = this._trackingCache.Get("DoesNotExist");
        Assert.Null(result3);
    }

    [Fact]
    public async Task LoadPopulatedFileAsync()
    {
        string trackingFile = Path.Combine(path1: this.TempFolder, $"{NewId}.json");
        this.Output.WriteLine(trackingFile);

        await File.WriteAllTextAsync(
            path: trackingFile,
            contents: "{\"Test1\":\"Hello World\",\"Test2\":\"Banana\"}",
            this.CancellationToken()
        );

        await this._trackingCache.LoadAsync(fileName: trackingFile, cancellationToken: this.CancellationToken());

        string? result = this._trackingCache.Get("Test1");

        Assert.Equal(expected: "Hello World", actual: result);

        string? result2 = this._trackingCache.Get("Test2");

        Assert.Equal(expected: "Banana", actual: result2);

        string? result3 = this._trackingCache.Get("DoesNotExist");

        Assert.Null(result3);
    }

    [Fact]
    public async Task SaveEmptyFileAsync()
    {
        string trackingFile = Path.Combine(path1: this.TempFolder, $"{NewId}.json");
        this.Output.WriteLine(trackingFile);

        this._trackingCache.Set(repoUrl: "Test1", value: "Hello World");
        this._trackingCache.Set(repoUrl: "Test1", value: null);

        await this._trackingCache.SaveAsync(fileName: trackingFile, cancellationToken: this.CancellationToken());

        string content = await File.ReadAllTextAsync(path: trackingFile, cancellationToken: this.CancellationToken());
        this.Output.WriteLine(content);

        Assert.Equal(expected: "{}", actual: content);
    }

    [Fact]
    public async Task SaveWithContentFileAsync()
    {
        string trackingFile = Path.Combine(path1: this.TempFolder, $"{NewId}.json");
        this.Output.WriteLine(trackingFile);

        this._trackingCache.Set(repoUrl: "Test1", value: "Hello World");
        this._trackingCache.Set(repoUrl: "Test2", value: "Banana");

        await this._trackingCache.SaveAsync(fileName: trackingFile, cancellationToken: this.CancellationToken());

        string content = await File.ReadAllTextAsync(path: trackingFile, cancellationToken: this.CancellationToken());
        this.Output.WriteLine(content);

        Assert.Equal(expected: "{\"Test1\":\"Hello World\",\"Test2\":\"Banana\"}", actual: content);
    }

    [Fact]
    public void SetSameValueDoesNotMarkChanged()
    {
        // Set an initial value, then set the same value again.
        // The second Set must return early without marking the cache as changed.
        string repoUrl = Guid.NewGuid().ToString();
        const string value = "SameValue";

        this._trackingCache.Set(repoUrl: repoUrl, value: value);
        this._trackingCache.Set(repoUrl: repoUrl, value: value);

        // The value must still be retrievable and unchanged.
        string? result = this._trackingCache.Get(repoUrl);
        Assert.Equal(expected: value, actual: result);
    }

    [Fact]
    public void SetDifferentValueUpdatesEntry()
    {
        // Set an initial value, then set a different value.
        // The entry must be updated to the new value.
        string repoUrl = Guid.NewGuid().ToString();
        const string initialValue = "InitialValue";
        const string updatedValue = "UpdatedValue";

        this._trackingCache.Set(repoUrl: repoUrl, value: initialValue);
        this._trackingCache.Set(repoUrl: repoUrl, value: updatedValue);

        string? result = this._trackingCache.Get(repoUrl);
        Assert.Equal(expected: updatedValue, actual: result);
    }

    [Fact]
    public void SetNullValueForNonExistentKeyDoesNothing()
    {
        // Setting null for a key that does not exist must leave the cache unchanged.
        string repoUrl = Guid.NewGuid().ToString();

        this._trackingCache.Set(repoUrl: repoUrl, value: null);

        string? result = this._trackingCache.Get(repoUrl);
        Assert.Null(result);
    }

    [Fact]
    public void SetWhitespaceValueForNonExistentKeyDoesNothing()
    {
        // Setting a whitespace value for a key that does not exist must leave the cache unchanged.
        string repoUrl = Guid.NewGuid().ToString();

        this._trackingCache.Set(repoUrl: repoUrl, value: "   ");

        string? result = this._trackingCache.Get(repoUrl);
        Assert.Null(result);
    }

    [Fact]
    public void SetWhitespaceValueForExistingKeyRemovesEntry()
    {
        // Setting a whitespace value for an existing key must remove the entry.
        string repoUrl = Guid.NewGuid().ToString();
        const string initialValue = "SomeValue";

        this._trackingCache.Set(repoUrl: repoUrl, value: initialValue);
        this._trackingCache.Set(repoUrl: repoUrl, value: "   ");

        string? result = this._trackingCache.Get(repoUrl);
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveDoesNotWriteFileWhenNothingHasChangedAsync()
    {
        // SaveAsync must return without writing a file when _changed is false.
        string trackingFile = Path.Combine(path1: this.TempFolder, $"{NewId}.json");
        this.Output.WriteLine(trackingFile);

        // Do not call Set — the cache has never been modified.
        await this._trackingCache.SaveAsync(fileName: trackingFile, cancellationToken: this.CancellationToken());

        Assert.False(
            condition: File.Exists(trackingFile),
            userMessage: "File must not be created when nothing has changed"
        );
    }

    [Fact]
    public async Task LoadJsonArrayThrowsAsync()
    {
        string trackingFile = Path.Combine(path1: this.TempFolder, $"{NewId}.json");
        this.Output.WriteLine(trackingFile);

        await File.WriteAllTextAsync(
            path: trackingFile,
            contents: "[1,2,3]",
            cancellationToken: this.CancellationToken()
        );

        await Assert.ThrowsAsync<JsonException>(() =>
            this._trackingCache.LoadAsync(fileName: trackingFile, this.CancellationToken()).AsTask()
        );
    }

    [Fact]
    public async Task LoadJsonWithNullValueThrowsAsync()
    {
        string trackingFile = Path.Combine(path1: this.TempFolder, $"{NewId}.json");
        this.Output.WriteLine(trackingFile);

        await File.WriteAllTextAsync(
            path: trackingFile,
            contents: "{\"key\":null}",
            cancellationToken: this.CancellationToken()
        );

        await Assert.ThrowsAsync<JsonException>(() =>
            this._trackingCache.LoadAsync(fileName: trackingFile, this.CancellationToken()).AsTask()
        );
    }
}
