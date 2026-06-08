using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Packages.Tests;

public sealed class PackageUpdateContextTests : TestBase
{
    private const string WORK_FOLDER = "/tmp/work";
    private const string CACHE_FILE = "/tmp/cache.json";
    private const string TRACKING_FILE = "/tmp/tracking.json";

    private static readonly DotNetVersionSettings DefaultDotNetSettings = new(
        SdkVersion: "9.0.100",
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

    [Fact]
    public void WorkFolderIsSet()
    {
        PackageUpdateContext context = CreateContext(workFolder: WORK_FOLDER);

        Assert.Equal(expected: WORK_FOLDER, actual: context.WorkFolder);
    }

    [Fact]
    public void CacheFileNameIsSet()
    {
        PackageUpdateContext context = CreateContext(cacheFileName: CACHE_FILE);

        Assert.Equal(expected: CACHE_FILE, actual: context.CacheFileName);
    }

    [Fact]
    public void CacheFileNameCanBeNull()
    {
        PackageUpdateContext context = CreateContext(cacheFileName: null);

        Assert.Null(context.CacheFileName);
    }

    [Fact]
    public void TrackingFileNameIsSet()
    {
        PackageUpdateContext context = CreateContext(trackingFileName: TRACKING_FILE);

        Assert.Equal(expected: TRACKING_FILE, actual: context.TrackingFileName);
    }

    [Fact]
    public void AdditionalSourcesAreSet()
    {
        IReadOnlyList<string> sources = ["https://example.com/nuget/v3/index.json"];
        PackageUpdateContext context = CreateContext(additionalSources: sources);

        Assert.Equal(expected: sources, actual: context.AdditionalSources);
    }

    [Fact]
    public void DotNetSettingsAreSet()
    {
        PackageUpdateContext context = CreateContext(dotNetSettings: DefaultDotNetSettings);

        Assert.Equal(expected: DefaultDotNetSettings, actual: context.DotNetSettings);
    }

    [Fact]
    public void ReleaseConfigIsSet()
    {
        PackageUpdateContext context = CreateContext(releaseConfig: DefaultReleaseConfig);

        Assert.Equal(expected: DefaultReleaseConfig, actual: context.ReleaseConfig);
    }

    private static PackageUpdateContext CreateContext(
        string workFolder = WORK_FOLDER,
        string? cacheFileName = CACHE_FILE,
        string trackingFileName = TRACKING_FILE,
        IReadOnlyList<string>? additionalSources = null,
        DotNetVersionSettings? dotNetSettings = null,
        ReleaseConfig? releaseConfig = null
    )
    {
        return new PackageUpdateContext(
            WorkFolder: workFolder,
            CacheFileName: cacheFileName,
            TrackingFileName: trackingFileName,
            AdditionalSources: additionalSources ?? [],
            DotNetSettings: dotNetSettings ?? DefaultDotNetSettings,
            ReleaseConfig: releaseConfig ?? DefaultReleaseConfig
        );
    }
}
