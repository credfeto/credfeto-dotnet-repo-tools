using System;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces.Tests.Exceptions;

public sealed class GitRepositoryLockedExceptionTests : TestBase
{
    [Fact]
    public void MustBeSealed()
    {
        Assert.True(
            typeof(GitRepositoryLockedException).IsSealed,
            userMessage: $"{nameof(GitRepositoryLockedException)} must be sealed"
        );
    }

    [Fact]
    public void MustDeriveFromException()
    {
        Assert.True(
            typeof(Exception).IsAssignableFrom(typeof(GitRepositoryLockedException)),
            userMessage: $"{nameof(GitRepositoryLockedException)} must derive from {nameof(Exception)}"
        );
    }

    [Fact]
    public void DefaultConstructorCreatesInstance()
    {
        GitRepositoryLockedException exception = new();

        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageConstructorSetsMessage()
    {
        const string message = "Repository is locked";

        GitRepositoryLockedException exception = new(message);

        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageConstructorWithNullMessageAccepted()
    {
        GitRepositoryLockedException exception = new(message: null);

        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsMessage()
    {
        const string message = "Repository is locked";
        InvalidOperationException inner = new("inner");

        GitRepositoryLockedException exception = new(message: message, innerException: inner);

        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsInnerException()
    {
        const string message = "Repository is locked";
        InvalidOperationException inner = new("inner");

        GitRepositoryLockedException exception = new(message: message, innerException: inner);

        Assert.Same(expected: inner, actual: exception.InnerException);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorWithNullsAccepted()
    {
        GitRepositoryLockedException exception = new(message: null, innerException: null);

        Assert.NotNull(exception);
    }
}
