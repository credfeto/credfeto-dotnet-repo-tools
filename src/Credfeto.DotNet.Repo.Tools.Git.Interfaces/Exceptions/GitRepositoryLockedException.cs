using System;

namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;

public sealed class GitRepositoryLockedException : Exception
{
    public GitRepositoryLockedException() { }

    public GitRepositoryLockedException(string? message)
        : base(message) { }

    public GitRepositoryLockedException(string? message, Exception? innerException)
        : base(message: message, innerException: innerException) { }
}
