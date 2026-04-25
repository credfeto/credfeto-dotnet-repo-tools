using System;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Exceptions;

public sealed class InvalidTemplateConfigException : Exception
{
    public InvalidTemplateConfigException() { }

    public InvalidTemplateConfigException(string? message)
        : base(message) { }

    public InvalidTemplateConfigException(string? message, Exception? innerException)
        : base(message: message, innerException: innerException) { }
}