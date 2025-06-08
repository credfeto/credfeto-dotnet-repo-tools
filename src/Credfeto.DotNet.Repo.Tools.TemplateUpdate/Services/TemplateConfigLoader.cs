using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class TemplateConfigLoader : ITemplateConfigLoader
{
    public ValueTask<TemplateConfig> LoadConfigAsync(string templatePath, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}