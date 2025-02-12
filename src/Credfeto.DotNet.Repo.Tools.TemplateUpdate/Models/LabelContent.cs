using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

[DebuggerDisplay("labels.yml: {Labels} - labeler.yml: {Labeler}")]
public readonly record struct LabelContent(string Labels, string Labeler);
