using System;

namespace Credfeto.DotNet.Repo.Tools.Build.Exceptions;

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