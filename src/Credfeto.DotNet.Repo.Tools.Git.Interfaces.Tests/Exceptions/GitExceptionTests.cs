using System;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces.Tests.Exceptions;

public sealed class GitExceptionTests : TestBase
{
    [Fact]
    public void MustBeSealed()
    {
        Assert.True(typeof(GitException).IsSealed, userMessage: $"{nameof(GitException)} must be sealed");
    }

    [Fact]
    public void MustDeriveFromException()
    {
        Assert.True(
            typeof(Exception).IsAssignableFrom(typeof(GitException)),
            userMessage: $"{nameof(GitException)} must derive from {nameof(Exception)}"
        );
    }

    [Fact]
    public void DefaultConstructorCreatesInstance()
    {
        GitException exception = new();

        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageConstructorSetsMessage()
    {
        const string message = "Git operation failed";

        GitException exception = new(message);

        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageConstructorWithNullMessageAccepted()
    {
        GitException exception = new(message: null);

        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsMessage()
    {
        const string message = "Git operation failed";
        InvalidOperationException inner = new("inner");

        GitException exception = new(message: message, innerException: inner);

        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsInnerException()
    {
        const string message = "Git operation failed";
        InvalidOperationException inner = new("inner");

        GitException exception = new(message: message, innerException: inner);

        Assert.Same(expected: inner, actual: exception.InnerException);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorWithNullsAccepted()
    {
        GitException exception = new(message: null, innerException: null);

        Assert.NotNull(exception);
    }
}
