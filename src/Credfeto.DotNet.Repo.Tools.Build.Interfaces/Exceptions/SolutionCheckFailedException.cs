using System;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;

public sealed class SolutionCheckFailedException : Exception
{
    public SolutionCheckFailedException() { }

    public SolutionCheckFailedException(string? message)
        : base(message) { }

    public SolutionCheckFailedException(string? message, Exception? innerException)
        : base(message: message, innerException: innerException) { }
}
