using System;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;

public sealed class SolutionCheckFailedException : Exception
{
    public SolutionCheckFailedException()
    {
    }

    public SolutionCheckFailedException(string? message)
        : base(message)
    {
    }

    public SolutionCheckFailedException(string? message, Exception? innerException)
        : base(message: message, innerException: innerException)
    {
    }
}