using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public interface ITemplateConfigLoader
{
    ValueTask<TemplateConfig> LoadConfigAsync(string templatePath, CancellationToken cancellationToken);
}