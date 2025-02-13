using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

[DebuggerDisplay(
    "Name: {Name}, Description: {Description}, Color: {Colour}, Paths: {Paths}, PathExclusions: {PathsExclude}"
)]
internal readonly record struct LabelConfig(
    string Name,
    string Description,
    string Colour,
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> PathsExclude
);
