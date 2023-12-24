using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Cmd.DotNet;

[DebuggerDisplay("SdkVersion: {SdkVersion} AllowPreRelease: {AllowPreRelease} rollForward: {RollForward}")]
public readonly record struct DotNetVersionSettings(string? SdkVersion, bool AllowPreRelease, string RollForward);