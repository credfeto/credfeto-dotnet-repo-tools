using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.DotNet.Repo.Tools.Cmd.DotNet;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Packages;

[DebuggerDisplay("Work: {WorkFolder} Additional Sources: {AdditionalSources.Count}")]
public sealed record UpdateContext(string WorkFolder, string? CacheFileName, string TrackingFileName, IReadOnlyList<string> AdditionalSources, DotNetVersionSettings DotNetSettings)
{
}