using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommandLine;

namespace Credfeto.Dotnet.Repo.Tools.Cmd;

[SuppressMessage(category: "ReSharper", checkId: "ClassNeverInstantiated.Global", Justification = "Created using reflection")]
[SuppressMessage(category: "ReSharper", checkId: "UnusedAutoPropertyAccessor.Global", Justification = "Created using reflection")]
public sealed class Options
{
    [Option(shortName: 'p', longName: "package-id", Required = true, HelpText = "Package Id to check for updates")]
    public string? PackageId { get; init; }

    [Option(shortName: 'f', longName: "folder", Required = true, HelpText = "Folder containing projects")]
    public string? Folder { get; init; }

    [Option(shortName: 'c', longName: "cache", Required = false, HelpText = "cache file")]
    public string? Cache { get; init; }

    [Option(shortName: 's', longName: "source", Required = false, HelpText = "Urls to additional NuGet feeds to load")]
    public IEnumerable<string>? Source { get; init; }

    [Option(shortName: 'x', longName: "exclude", Required = false, HelpText = "Package Ids to exclude from the update")]
    public IEnumerable<string>? Exclude { get; init; }
}