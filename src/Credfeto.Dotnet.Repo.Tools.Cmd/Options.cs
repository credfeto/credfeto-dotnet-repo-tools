using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommandLine;

namespace Credfeto.Dotnet.Repo.Tools.Cmd;

[SuppressMessage(category: "ReSharper", checkId: "ClassNeverInstantiated.Global", Justification = "Created using reflection")]
[SuppressMessage(category: "ReSharper", checkId: "UnusedAutoPropertyAccessor.Global", Justification = "Created using reflection")]
public sealed class Options
{
    [Option(shortName: 'r', longName: "repositories", Required = true, HelpText = "repos.lst file containing list of repositories")]
    public string? Repositories { get; init; }

    [Option(shortName: 'w', longName: "work", Required = true, HelpText = "folder where to clone repositories")]
    public string? Work { get; init; }

    [Option(shortName: 't', longName: "tracking", Required = true, HelpText = "folder where to write tracking.json file")]
    public string? Tracking { get; init; }

    [Option(shortName: 'p', longName: "packages", Required = true, HelpText = "Packages.json file to load")]
    public string? Packages { get; init; }

    [Option(shortName: 'c', longName: "cache", Required = true, HelpText = "package cache file")]
    public string? Cache { get; init; }

    [Option(shortName: 's', longName: "source", Required = false, HelpText = "Urls to additional NuGet feeds to load")]
    public IEnumerable<string>? Source { get; init; }

    // [Option(shortName: 'p', longName: "package-id", Required = true, HelpText = "Package Id to check for updates")]
    // public string? PackageId { get; init; }
    //
    // [Option(shortName: 'f', longName: "folder", Required = true, HelpText = "Folder containing projects")]
    // public string? Folder { get; init; }
    //
    // [Option(shortName: 'c', longName: "cache", Required = false, HelpText = "cache file")]
    // public string? Cache { get; init; }
    //
    //
    // [Option(shortName: 'x', longName: "exclude", Required = false, HelpText = "Package Ids to exclude from the update")]
    // public IEnumerable<string>? Exclude { get; init; }
}