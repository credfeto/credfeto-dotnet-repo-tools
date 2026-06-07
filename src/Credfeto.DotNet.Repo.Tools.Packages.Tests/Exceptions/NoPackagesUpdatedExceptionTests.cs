using System;
using Credfeto.DotNet.Repo.Tools.Packages.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Packages.Tests.Exceptions;

public sealed class NoPackagesUpdatedExceptionTests : TestBase
{
    [Fact]
    public void MustBeSealed()
    {
        Assert.True(
            typeof(NoPackagesUpdatedException).IsSealed,
            userMessage: $"{nameof(NoPackagesUpdatedException)} must be sealed"
        );
    }

    [Fact]
    public void MustDeriveFromException()
    {
        Assert.True(
            typeof(Exception).IsAssignableFrom(typeof(NoPackagesUpdatedException)),
            userMessage: $"{nameof(NoPackagesUpdatedException)} must derive from {nameof(Exception)}"
        );
    }

    [Fact]
    public void DefaultConstructorCreatesInstance()
    {
        NoPackagesUpdatedException exception = new();

        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageConstructorSetsMessage()
    {
        const string message = "No packages were updated";

        NoPackagesUpdatedException exception = new(message);

        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageConstructorWithNullMessageAccepted()
    {
        NoPackagesUpdatedException exception = new(message: null);

        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsMessage()
    {
        const string message = "No packages were updated";
        InvalidOperationException inner = new("inner");

        NoPackagesUpdatedException exception = new(message: message, innerException: inner);

        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsInnerException()
    {
        const string message = "No packages were updated";
        InvalidOperationException inner = new("inner");

        NoPackagesUpdatedException exception = new(message: message, innerException: inner);

        Assert.Same(expected: inner, actual: exception.InnerException);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorWithNullsAccepted()
    {
        NoPackagesUpdatedException exception = new(message: null, innerException: null);

        Assert.NotNull(exception);
    }
}
