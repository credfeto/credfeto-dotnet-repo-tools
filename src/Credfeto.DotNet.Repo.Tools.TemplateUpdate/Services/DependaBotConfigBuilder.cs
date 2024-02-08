using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class DependaBotConfigBuilder : IDependaBotConfigBuilder
{
    private readonly ILogger<DependaBotConfigBuilder> _logger;

    public DependaBotConfigBuilder(ILogger<DependaBotConfigBuilder> logger)
    {
        this._logger = logger;
    }

    public ValueTask<string> BuildDependabotConfigAsync(RepoContext repoContext, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(string.Empty);
    }
}