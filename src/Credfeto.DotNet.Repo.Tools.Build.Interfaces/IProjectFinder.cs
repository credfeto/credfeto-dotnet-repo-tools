using System.Collections.Generic;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces;

public interface IProjectFinder
{
    ValueTask<IReadOnlyList<string>> FindProjectsAsync(string basePath);
}