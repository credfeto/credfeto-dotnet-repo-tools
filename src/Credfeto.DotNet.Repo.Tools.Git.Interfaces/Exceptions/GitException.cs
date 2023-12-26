using System;

namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;

public sealed class GitException : Exception
{
    public GitException()
    {
    }

    public GitException(string? message)
        : base(message)
    {
    }

    public GitException(string? message, Exception? innerException)
        : base(message: message, innerException: innerException)
    {
    }
}