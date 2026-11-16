using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;

[DebuggerDisplay("SdkVersion: {SdkVersion} AllowPreRelease: {AllowPreRelease}")]
public readonly record struct DotNetVersionSettings(string? SdkVersion, bool AllowPreRelease);
