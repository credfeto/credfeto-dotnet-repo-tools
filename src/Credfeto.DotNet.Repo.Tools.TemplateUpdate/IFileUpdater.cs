using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public interface IFileUpdater
{
    ValueTask<bool> UpdateFileAsync(RepoContext repoContext, CopyInstruction copyInstruction, Func<CancellationToken, ValueTask> changelogUpdate, CancellationToken cancellationToken);
}
