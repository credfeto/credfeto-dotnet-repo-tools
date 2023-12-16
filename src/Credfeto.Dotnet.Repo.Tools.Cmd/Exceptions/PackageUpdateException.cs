using System;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;

public sealed class PackageUpdateException : Exception
{
    public PackageUpdateException()
    {
    }

    public PackageUpdateException(string? message)
        : base(message)
    {
    }

    public PackageUpdateException(string? message, Exception? innerException)
        : base(message: message, innerException: innerException)
    {
    }
}