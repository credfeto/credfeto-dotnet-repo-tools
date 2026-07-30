using System;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Extensions.Tests;

public sealed class UriExtensionsTests : TestBase
{
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    public void IsHttpReturnsTrueForHttpSchemes(string uri)
    {
        Assert.True(new Uri(uri).IsHttp(), $"Expected {uri} to be recognised as HTTP(S)");
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///path")]
    [InlineData("git://github.com/repo")]
    public void IsHttpReturnsFalseForNonHttpSchemes(string uri)
    {
        Assert.False(new Uri(uri).IsHttp(), $"Expected {uri} to not be recognised as HTTP(S)");
    }
}
