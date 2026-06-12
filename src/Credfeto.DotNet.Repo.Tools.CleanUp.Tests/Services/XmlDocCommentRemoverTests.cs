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

    [Fact]
    public void RemoveXmlDocCommentsShouldReturnInputUnchangedForEmptyString()
    {
        const string input = "";
        string result = this._xmlDocCommentRemover.RemoveXmlDocComments(input);
        Assert.Equal(expected: input, actual: result);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("/// <summary>This is a doc comment</summary>")]
    [InlineData("/// <param name=\"x\">Parameter</param>")]
    [InlineData("public void Method() { }")]
    public void RemoveXmlDocCommentsShouldReturnInputUnchanged(string input)
    {
        string result = this._xmlDocCommentRemover.RemoveXmlDocComments(input);
        Assert.Equal(expected: input, actual: result);
    }
}
