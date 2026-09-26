using System;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

public sealed class XmlDocCommentRemoverTests : TestBase
{
    private readonly IXmlDocCommentRemover _xmlDocCommentRemover;

    public XmlDocCommentRemoverTests()
    {
        this._xmlDocCommentRemover = new XmlDocCommentRemover();
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello world")]
    [InlineData("public void Method() { }")]
    [InlineData("public class Example\n{\n    // regular comment\n    public int Value { get; }\n}\n")]
    [InlineData("public class Example\n{\n    /* block comment */\n    public int Value { get; }\n}\n")]
    [InlineData("public class Example\n{\n    //// commented out code\n    public int Value { get; }\n}\n")]
    [InlineData("public class Example\n{\n    /**/\n    public int Value { get; }\n}\n")]
    [InlineData("public class Example\n{\n    public string Value => \"/// not a comment\";\n}\n")]
    [InlineData("public class Example\n{\n    public string Value => \"/** not a comment */\";\n}\n")]
    [InlineData("public class Example\n{\n    public string Value => @\"\n/// not a comment\n\";\n}\n")]
    public void RemoveXmlDocCommentsShouldReturnInputUnchanged(string input)
    {
        string result = this._xmlDocCommentRemover.RemoveXmlDocComments(input);

        Assert.Equal(expected: input, actual: result);
    }

    [Theory]
    [InlineData("/// <summary>A class</summary>\npublic class Example { }\n", "public class Example { }\n")]
    [InlineData(
        "/// <summary>\n/// A class.\n/// </summary>\npublic sealed class Example { }\n",
        "public sealed class Example { }\n"
    )]
    [InlineData(
        "public class Example\n{\n    /// <summary>Does it</summary>\n    public void Method() { }\n}\n",
        "public class Example\n{\n    public void Method() { }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    /// <summary>The value</summary>\n    public int Value { get; }\n}\n",
        "public class Example\n{\n    public int Value { get; }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    /// <summary>\n    /// Does it.\n    /// </summary>\n    /// <param name=\"x\">The x</param>\n    /// <returns>The result</returns>\n    public int Method(int x) => x;\n}\n",
        "public class Example\n{\n    public int Method(int x) => x;\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    /// <inheritdoc/>\n    public override string ToString() => \"x\";\n}\n",
        "public class Example\n{\n    public override string ToString() => \"x\";\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    /// <inheritdoc />\n    public override string ToString() => \"x\";\n}\n",
        "public class Example\n{\n    public override string ToString() => \"x\";\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    /// <summary>Do it</summary>\n    /// <param name=\"x\">The x</param>\n    public void First(int x) { }\n\n    /// <summary>Do that</summary>\n    public void Second() { }\n}\n",
        "public class Example\n{\n    public void First(int x) { }\n\n    public void Second() { }\n}\n"
    )]
    [InlineData("/** A class */\npublic class Example { }\n", "public class Example { }\n")]
    [InlineData(
        "public class Example\n{\n    /**\n     * <summary>Does it</summary>\n     * <param name=\"x\">The x</param>\n     */\n    public void Method(int x) { }\n}\n",
        "public class Example\n{\n    public void Method(int x) { }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    /** <summary>The value</summary> */\n    public int Value { get; }\n}\n",
        "public class Example\n{\n    public int Value { get; }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    /** <summary>The value</summary> */ public int Value { get; }\n}\n",
        "public class Example\n{\n    public int Value { get; }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    public int/** x */Value { get; }\n}\n",
        "public class Example\n{\n    public int Value { get; }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    /// <summary>Old</summary>\n    [System.Obsolete]\n    public void Method() { }\n}\n",
        "public class Example\n{\n    [System.Obsolete]\n    public void Method() { }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    [System.Obsolete]\n    /// <summary>Old</summary>\n    public void Method() { }\n}\n",
        "public class Example\n{\n    [System.Obsolete]\n    public void Method() { }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n#if !SOME_SYMBOL\n    /// <summary>Conditional</summary>\n    public void Method() { }\n#endif\n}\n",
        "public class Example\n{\n#if !SOME_SYMBOL\n    public void Method() { }\n#endif\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    public int Value { get; } /// <summary>Trailing</summary>\n    public int Other { get; }\n}\n",
        "public class Example\n{\n    public int Value { get; }\n    public int Other { get; }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    // regular comment\n    /// <summary>Doc</summary>\n    /* block comment */\n    public void Method() { }\n}\n",
        "public class Example\n{\n    // regular comment\n    /* block comment */\n    public void Method() { }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    //// commented out code\n    /// <summary>Doc</summary>\n    public void Method() { }\n}\n",
        "public class Example\n{\n    //// commented out code\n    public void Method() { }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n    public string Value => \"/// text\"; /// <summary>Doc</summary>\n}\n",
        "public class Example\n{\n    public string Value => \"/// text\";\n}\n"
    )]
    [InlineData("/// <summary>unclosed & <b>bad</c>\npublic class Example { }\n", "public class Example { }\n")]
    [InlineData(
        "public class Example\n{\n    /// <summary\n    /// <param name=\"x>\n    public void Method() { }\n}\n",
        "public class Example\n{\n    public void Method() { }\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n#if NET8_0\n    /// <summary>Conditional</summary>\n    public void Method() { }\n#else\n    /// <summary>Other</summary>\n    public void Method(int x) { }\n#endif\n}\n",
        "public class Example\n{\n#if NET8_0\n    public void Method() { }\n#else\n    public void Method(int x) { }\n#endif\n}\n"
    )]
    [InlineData(
        "public class Example\n{\n#if NET8_0\n    //// commented out\n    /// <summary>Conditional</summary>\n    /// <returns>x</returns>\n    public void Method() { }\n#endif\n}\n",
        "public class Example\n{\n#if NET8_0\n    //// commented out\n    public void Method() { }\n#endif\n}\n"
    )]
    [InlineData("public class Example { }\n/// <summary>Dangling</summary>", "public class Example { }\n")]
    [InlineData("public class Example { }\n/// <summary>Dangling</summary>\n", "public class Example { }\n")]
    public void RemoveXmlDocCommentsShouldRemoveDocCommentsAndKeepEverythingElse(string input, string expected)
    {
        string result = this._xmlDocCommentRemover.RemoveXmlDocComments(input);

        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void RemoveXmlDocCommentsShouldPreserveLineEndings(string lineEnding)
    {
        string input =
            "public class Example\n{\n    /// <summary>\n    /// The value\n    /// </summary>\n    public int Value { get; }\n\n    /** <summary>Other</summary> */\n    public int Other { get; }\n#if NET8_0\n    /// <summary>Third</summary>\n    public int Third { get; }\n#endif\n}\n".Replace(
                oldValue: "\n",
                newValue: lineEnding,
                comparisonType: StringComparison.Ordinal
            );
        string expected =
            "public class Example\n{\n    public int Value { get; }\n\n    public int Other { get; }\n#if NET8_0\n    public int Third { get; }\n#endif\n}\n".Replace(
                oldValue: "\n",
                newValue: lineEnding,
                comparisonType: StringComparison.Ordinal
            );

        string result = this._xmlDocCommentRemover.RemoveXmlDocComments(input);

        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData("/// <summary>A class</summary>\npublic class Example { }\n")]
    [InlineData("public class Example\n{\n    /** <summary>The value</summary> */\n    public int Value { get; }\n}\n")]
    [InlineData("public class Example\n{\n    /// <inheritdoc/>\n    public override string ToString() => \"x\";\n}\n")]
    public void RemoveXmlDocCommentsShouldBeIdempotent(string input)
    {
        string once = this._xmlDocCommentRemover.RemoveXmlDocComments(input);

        string twice = this._xmlDocCommentRemover.RemoveXmlDocComments(once);

        Assert.Equal(expected: once, actual: twice);
    }

    [Theory]
    [InlineData("/// <summary>A class</summary>\npublic class Example { }\n")]
    [InlineData("public class Example\n{\n    /** <summary>The value</summary> */ public int Value { get; }\n}\n")]
    [InlineData("public class Example\n{\n    public int/** x */Value { get; }\n}\n")]
    public void RemoveXmlDocCommentsShouldProduceCodeThatStillParses(string input)
    {
        string result = this._xmlDocCommentRemover.RemoveXmlDocComments(input);

        int errors = RoslynSyntaxValidation.CountSyntaxErrors(
            content: result,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 0, actual: errors);
    }
}
