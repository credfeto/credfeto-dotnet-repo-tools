using System.Diagnostics;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

[DebuggerDisplay("Work: {WorkFolder}")]
public readonly record struct TemplateUpdateContext(
    string WorkFolder,
    string TemplateFolder,
    string TrackingFileName,
    TemplateConfig TemplateConfig,
    DotNetVersionSettings DotNetSettings,
    ReleaseConfig ReleaseConfig);