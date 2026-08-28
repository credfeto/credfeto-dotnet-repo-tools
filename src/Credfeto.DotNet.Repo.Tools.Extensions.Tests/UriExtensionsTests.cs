using System;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Extensions.Tests;

public sealed class UriExtensionsTests : TestBase
{
    [Theory]
    [InlineData("http://example.com", true)]
    [InlineData("https://example.com", true)]
    [InlineData("ftp://example.com", false)]
    [InlineData("file:///path", false)]
    [InlineData("git://github.com/repo", false)]
    public static void IsHttp(string uri, bool expected)
    {
        Assert.Equal(expected: expected, actual: new Uri(uri).IsHttp());
    }
}
