using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.Date.Interfaces;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

[DebuggerDisplay("Work: {WorkFolder} Additional Sources: {AdditionalSources.Count}")]
public sealed record UpdateContext(string WorkFolder, string? Cache, string Tracking, IReadOnlyList<string> AdditionalSources, ICurrentTimeSource TimeSource);