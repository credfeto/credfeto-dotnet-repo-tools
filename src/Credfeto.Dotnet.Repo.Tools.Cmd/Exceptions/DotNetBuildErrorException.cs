using System;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;

public sealed class DotNetBuildErrorException : Exception
{
    public DotNetBuildErrorException()
    {
    }

    public DotNetBuildErrorException(string? message)
        : base(message)
    {
    }

    public DotNetBuildErrorException(string? message, Exception? innerException)
        : base(message: message, innerException: innerException)
    {
    }
}