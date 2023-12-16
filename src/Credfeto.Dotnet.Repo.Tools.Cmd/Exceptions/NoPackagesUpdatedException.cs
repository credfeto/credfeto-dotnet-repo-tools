using System;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;

public sealed class NoPackagesUpdatedException : Exception
{
    public NoPackagesUpdatedException()
    {
    }

    public NoPackagesUpdatedException(string? message)
        : base(message)
    {
    }

    public NoPackagesUpdatedException(string? message, Exception? innerException)
        : base(message: message, innerException: innerException)
    {
    }
}