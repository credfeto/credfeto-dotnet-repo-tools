using System.Collections.Generic;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public interface ILabelsBuilder
{
    (string labels, string labeler) BuildLabelsConfig(IReadOnlyList<string> projects);
}