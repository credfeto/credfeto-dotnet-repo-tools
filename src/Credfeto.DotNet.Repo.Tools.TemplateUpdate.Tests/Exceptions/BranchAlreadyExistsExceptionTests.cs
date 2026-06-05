using System;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Exceptions;

public sealed class BranchAlreadyExistsExceptionTests : TestBase
{
    [Fact]
    public void DefaultConstructorCreatesException()
    {
        BranchAlreadyExistsException exception = new();
        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageConstructorSetsMessage()
    {
        const string message = "Branch already exists";
        BranchAlreadyExistsException exception = new(message);
        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsMessageAndInnerException()
    {
        const string message = "Branch already exists";
        InvalidOperationException innerException = new("inner");
        BranchAlreadyExistsException exception = new(message: message, innerException: innerException);
        Assert.Equal(expected: message, actual: exception.Message);
        Assert.Same(expected: innerException, actual: exception.InnerException);
    }
}
