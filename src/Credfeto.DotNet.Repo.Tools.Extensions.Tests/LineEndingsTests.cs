using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Extensions.Tests;

public sealed class LineEndingsTests : TestBase
{
    private const string LF = "\n";
    private const string CRLF = "\r\n";

    [Theory]
    [InlineData("", null)]
    [InlineData("no newline", null)]
    [InlineData("a\nb\n", LF)]
    [InlineData("a\r\nb\r\n", CRLF)]
    [InlineData("\nb", LF)]
    [InlineData("a\r\nb\nc\n", LF)]
    [InlineData("a\nb\r\nc\r\n", CRLF)]
    [InlineData("a\r\nb\n", LF)]
    public static void DetectDominant(string content, string? expected)
    {
        Assert.Equal(expected: expected, actual: LineEndings.DetectDominant(content));
    }

    [Theory]
    [InlineData("a\r\nb\nc", LF, "a\nb\nc")]
    [InlineData("a\r\nb\nc", CRLF, "a\r\nb\r\nc")]
    [InlineData("a\rb", CRLF, "a\rb")]
    public static void Normalise(string content, string newLine, string expected)
    {
        Assert.Equal(expected: expected, actual: LineEndings.Normalise(content: content, newLine: newLine));
    }

    [Theory]
    [InlineData("a\nb\n", "x\r\ny\r\n", "x\ny\n")]
    [InlineData("a\r\nb\r\n", "x\ny\n", "x\r\ny\r\n")]
    [InlineData("a", "x\r\ny\n", "x\r\ny\n")]
    public static void MatchOriginal(string original, string updated, string expected)
    {
        Assert.Equal(expected: expected, actual: LineEndings.MatchOriginal(original: original, updated: updated));
    }
}
