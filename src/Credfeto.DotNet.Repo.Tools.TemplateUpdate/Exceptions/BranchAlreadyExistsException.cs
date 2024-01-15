using System;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Exceptions;

public sealed class BranchAlreadyExistsException : Exception
{
    public BranchAlreadyExistsException()
    {
    }

    public BranchAlreadyExistsException(string? message)
        : base(message)
    {
    }

    public BranchAlreadyExistsException(string? message, Exception? innerException)
        : base(message: message, innerException: innerException)
    {
    }
}