using System;
using Credfeto.DotNet.Repo.Tools.Packages.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Packages.Tests.Exceptions;

public sealed class PackageUpdateExceptionTests : TestBase
{
    [Fact]
    public void MustBeSealed()
    {
        Assert.True(
            typeof(PackageUpdateException).IsSealed,
            userMessage: $"{nameof(PackageUpdateException)} must be sealed"
        );
    }

    [Fact]
    public void MustDeriveFromException()
    {
        Assert.True(
            typeof(Exception).IsAssignableFrom(typeof(PackageUpdateException)),
            userMessage: $"{nameof(PackageUpdateException)} must derive from {nameof(Exception)}"
        );
    }

    [Fact]
    public void DefaultConstructorCreatesInstance()
    {
        PackageUpdateException exception = new();

        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageConstructorSetsMessage()
    {
        const string message = "Package update failed";

        PackageUpdateException exception = new(message);

        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageConstructorWithNullMessageAccepted()
    {
        PackageUpdateException exception = new(message: null);

        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsMessage()
    {
        const string message = "Package update failed";
        InvalidOperationException inner = new("inner");

        PackageUpdateException exception = new(message: message, innerException: inner);

        Assert.Equal(expected: message, actual: exception.Message);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsInnerException()
    {
        const string message = "Package update failed";
        InvalidOperationException inner = new("inner");

        PackageUpdateException exception = new(message: message, innerException: inner);

        Assert.Same(expected: inner, actual: exception.InnerException);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorWithNullsAccepted()
    {
        PackageUpdateException exception = new(message: null, innerException: null);

        Assert.NotNull(exception);
    }
}
