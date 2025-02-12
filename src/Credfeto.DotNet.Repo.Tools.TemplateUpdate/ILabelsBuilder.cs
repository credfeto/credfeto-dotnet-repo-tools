using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public interface ILabelsBuilder
{
    LabelContent BuildLabelsConfig(IReadOnlyList<string> projects);
}
