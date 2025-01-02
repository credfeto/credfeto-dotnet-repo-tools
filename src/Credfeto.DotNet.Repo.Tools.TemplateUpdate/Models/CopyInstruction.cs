using System;
using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

[DebuggerDisplay("Copy {SourceFileName} to {TargetFileName} => {Message}")]
public readonly record struct CopyInstruction(string SourceFileName, string TargetFileName, Func<byte[], (byte[] bytes, bool changed)> Apply, Func<byte[], byte[], bool> IsTargetNewer, string Message);