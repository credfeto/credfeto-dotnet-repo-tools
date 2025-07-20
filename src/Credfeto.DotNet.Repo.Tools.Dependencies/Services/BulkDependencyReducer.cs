using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Services;

public sealed class BulkDependencyReducer : IBulkDependencyReducer
{
    private readonly IDependencyReducer _dependencyReducer;
    private readonly ILogger<BulkDependencyReducer> _logger;

    public BulkDependencyReducer(IDependencyReducer dependencyReducer, ILogger<BulkDependencyReducer> logger)
    {
        this._dependencyReducer = dependencyReducer;
        this._logger = logger;
    }

    public async ValueTask BulkUpdateAsync(string templateRepository,
                                           string trackingFileName,
                                           string packagesFileName,
                                           string workFolder,
                                           string releaseConfigFileName,
                                           IReadOnlyList<string> repositories,
                                           CancellationToken cancellationToken)
    {
        ReferenceConfig config = new();

        bool result = await this._dependencyReducer.CheckReferencesAsync(sourceDirectory: workFolder, config: config, cancellationToken: cancellationToken);

        Debug.WriteLine(result);
    }
}