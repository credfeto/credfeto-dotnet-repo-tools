using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

[DebuggerDisplay("{Name}: {AppliesTo}")]
public readonly record struct DependabotGroup(string Name, string AppliesTo, IReadOnlyList<string> Patterns);
