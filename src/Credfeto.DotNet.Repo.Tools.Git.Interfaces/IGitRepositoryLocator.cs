namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces;

public interface IGitRepositoryLocator
{
    string GetWorkingDirectory(string workDir, string repoUrl);
}