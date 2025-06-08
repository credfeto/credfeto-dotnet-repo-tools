using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

[DebuggerDisplay("Banana")]
public readonly record struct TemplateConfig(string TemplateName, string TemplatePath);