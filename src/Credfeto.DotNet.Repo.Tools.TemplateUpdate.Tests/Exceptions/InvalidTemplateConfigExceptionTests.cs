using System;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Exceptions;

public sealed class InvalidTemplateConfigExceptionTests : TestBase
{
    [Fact]
    public void DefaultConstructorCreatesException()
    {
        InvalidTemplateConfigException exception = new();
        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageConstructorSetsMessage()
    {
        const string message = "Invalid template config";
        InvalidTemplateConfigException exception = new(message);
        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsMessageAndInnerException()
    {
        const string message = "Invalid template config";
        InvalidOperationException innerException = new("inner");
        InvalidTemplateConfigException exception = new(message: message, innerException: innerException);
        Assert.Equal(expected: message, actual: exception.Message);
        Assert.Same(expected: innerException, actual: exception.InnerException);
    }
}
