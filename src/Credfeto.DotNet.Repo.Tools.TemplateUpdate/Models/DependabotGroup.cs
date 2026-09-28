using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

[DebuggerDisplay("{Name}")]
internal readonly record struct DependabotGroup(string Name, IReadOnlyList<string> Patterns);
