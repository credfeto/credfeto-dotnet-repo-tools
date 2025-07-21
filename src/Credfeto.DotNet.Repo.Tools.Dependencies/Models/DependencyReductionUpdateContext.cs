using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

[DebuggerDisplay("Work: {WorkFolder} Additional Sources: {AdditionalSources.Count}")]
public readonly record struct DependencyReductionUpdateContext(string WorkFolder, string TrackingFileName, IReadOnlyList<string> AdditionalSources, DotNetVersionSettings DotNetSettings);