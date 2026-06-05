using System;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Release.Interfaces.Tests.Exceptions;

public sealed class ReleaseCreatedExceptionTests : TestBase
{
    [Fact]
    public void MustBeSealed()
    {
        Assert.True(
            typeof(ReleaseCreatedException).IsSealed,
            userMessage: $"{nameof(ReleaseCreatedException)} must be sealed"
        );
    }

    [Fact]
    public void MustDeriveFromException()
    {
        Assert.True(
            typeof(Exception).IsAssignableFrom(typeof(ReleaseCreatedException)),
            userMessage: $"{nameof(ReleaseCreatedException)} must derive from {nameof(Exception)}"
        );
    }

    [Fact]
    public void DefaultConstructorCreatesInstance()
    {
        ReleaseCreatedException exception = new();

        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageConstructorSetsMessage()
    {
        const string message = "Release was created";

        ReleaseCreatedException exception = new(message);

        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageConstructorWithNullMessageAccepted()
    {
        ReleaseCreatedException exception = new(message: null);

        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsMessage()
    {
        const string message = "Release was created";
        InvalidOperationException inner = new("inner");

        ReleaseCreatedException exception = new(message: message, innerException: inner);

        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsInnerException()
    {
        const string message = "Release was created";
        InvalidOperationException inner = new("inner");

        ReleaseCreatedException exception = new(message: message, innerException: inner);

        Assert.Same(expected: inner, actual: exception.InnerException);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorWithNullsAccepted()
    {
        ReleaseCreatedException exception = new(message: null, innerException: null);

        Assert.NotNull(exception);
    }
}
