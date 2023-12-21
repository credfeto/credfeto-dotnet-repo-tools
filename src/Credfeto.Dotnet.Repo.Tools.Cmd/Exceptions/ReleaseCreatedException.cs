using System;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;

public sealed class ReleaseCreatedException : Exception
{
    public ReleaseCreatedException()
    {
    }

    public ReleaseCreatedException(string? message)
        : base(message)
    {
    }

    public ReleaseCreatedException(string? message, Exception? innerException)
        : base(message: message, innerException: innerException)
    {
    }
}