using System.Diagnostics;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

[DebuggerDisplay("Work: {WorkFolder}")]
public readonly record struct DependencyReductionUpdateContext(
    string WorkFolder,
    string TrackingFileName,
    DotNetVersionSettings DotNetSettings
);
