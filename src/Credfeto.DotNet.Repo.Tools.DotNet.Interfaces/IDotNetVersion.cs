using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;

public interface IDotNetVersion
{
    ValueTask<IReadOnlyList<Version>> GetInstalledSdksAsync(CancellationToken cancellationToken);
}
