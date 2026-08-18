using System;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

public sealed class ResharperSuppressionToSuppressMessageTests : TestBase
{
    private readonly IResharperSuppressionToSuppressMessage _resharperSuppressionToSuppressMessage;

    public ResharperSuppressionToSuppressMessageTests()
    {
        this._resharperSuppressionToSuppressMessage = new ResharperSuppressionToSuppressMessage();
    }

    private static string MakeDisable(string item)
    {
        return "// ReSharper disable once " + item;
    }

    private static string MakeSuppressMessage(string item)
    {
        return "[System.Diagnostics.CodeAnalysis.SuppressMessage(\"ReSharper\", \""
            + item
            + "\", Justification=\"TODO: Review\")]";
    }

    [Fact]
    public void Replace1()
    {
        string input =
            MakeDisable("RedundantDefaultMemberInitializer")
            + @"
            public void Example()
            {
                // Simple
            }
";

        const string expected =
            @"[System.Diagnostics.CodeAnalysis.SuppressMessage(""ReSharper"", ""RedundantDefaultMemberInitializer"", Justification=""TODO: Review"")]
            public void Example()
            {
                // Simple
            }
";

        this.TestIt(input: input, expected: expected);
    }

    [Theory]
    [InlineData("RedundantDefaultMemberInitializer")]
    [InlineData("ParameterOnlyUsedForPreconditionCheck.Global")]
    [InlineData("ParameterOnlyUsedForPreconditionCheck.Local")]
    [InlineData("UnusedMember.Global")]
    [InlineData("UnusedMember.Local")]
    [InlineData("AutoPropertyCanBeMadeGetOnly.Global")]
    [InlineData("AutoPropertyCanBeMadeGetOnly.Local")]
    [InlineData("ClassNeverInstantiated.Local")]
    [InlineData("ClassNeverInstantiated.Global")]
    [InlineData("ClassCanBeSealed.Global")]
    [InlineData("ClassCanBeSealed.Local")]
    [InlineData("UnusedAutoPropertyAccessor.Global")]
    [InlineData("UnusedAutoPropertyAccessor.Local")]
    [InlineData("MemberCanBePrivate.Global")]
    [InlineData("MemberCanBePrivate.Local")]
    [InlineData("InconsistentNaming")]
    [InlineData("IdentifierTypo")]
    [InlineData("UnusedTypeParameter")]
    [InlineData("HeapView.BoxingAllocation")]
    [InlineData("UnusedType.Local")]
    [InlineData("UnusedType.Global")]
    [InlineData("PrivateFieldCanBeConvertedToLocalVariable")]
    public void ReplaceShouldReplaceKnownRuleWithSuppressMessageAttribute(string rule)
    {
        string input = MakeDisable(rule) + "\n    public void Example() { }";
        string expectedSuppressMessage = MakeSuppressMessage(rule);

        string result = this._resharperSuppressionToSuppressMessage.Replace(input);

        Assert.Contains(expectedSuppressMessage, result, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceShouldNotReplaceUnknownRule()
    {
        const string rule = "SomeUnknownRule";
        string input = MakeDisable(rule) + "\n    public void Example() { }";

        string result = this._resharperSuppressionToSuppressMessage.Replace(input);

        Assert.Equal(expected: input, actual: result);
    }

    [Fact]
    public void ReplaceShouldReturnEmptyStringUnchanged()
    {
        string result = this._resharperSuppressionToSuppressMessage.Replace(string.Empty);
        Assert.Equal(expected: string.Empty, actual: result);
    }

    [Fact]
    public void ReplaceShouldNotChangeCodeWithNoResharperComments()
    {
        const string input = "public void Method() { }";
        string result = this._resharperSuppressionToSuppressMessage.Replace(input);
        Assert.Equal(expected: input, actual: result);
    }

    [Fact]
    public void ReplaceShouldNotConvertCommentAboveLocalVariableDeclaration()
    {
        string input =
            "public sealed class Example\n{\n    public void Method()\n    {\n        "
            + MakeDisable("InconsistentNaming")
            + "\n        int x = 5;\n    }\n}";

        this.TestIt(input: input, expected: input);
    }

    [Fact]
    public void ReplaceShouldNotConvertTrailingSameLineComment()
    {
        string input =
            "public sealed class Example\n{\n    public void Method()\n    {\n        int x = 5; "
            + MakeDisable("InconsistentNaming")
            + "\n    }\n}";

        this.TestIt(input: input, expected: input);
    }

    [Fact]
    public void ReplaceShouldConvertOnlyLegalOccurrencesWhenFileHasMixedPositions()
    {
        string input =
            "public sealed class Example\n{\n    "
            + MakeDisable("InconsistentNaming")
            + "\n    public void Method()\n    {\n        "
            + MakeDisable("InconsistentNaming")
            + "\n        int x = 5;\n    }\n}";

        string expected =
            "public sealed class Example\n{\n    "
            + MakeSuppressMessage("InconsistentNaming")
            + "\n    public void Method()\n    {\n        "
            + MakeDisable("InconsistentNaming")
            + "\n        int x = 5;\n    }\n}";

        this.TestIt(input: input, expected: expected);
    }

    [Fact]
    public void ReplaceShouldPreserveIndentationOfConvertedComment()
    {
        string input =
            "public sealed class Example\n{\n    "
            + MakeDisable("InconsistentNaming")
            + "\n    public void Method() { }\n}";

        string expected =
            "public sealed class Example\n{\n    "
            + MakeSuppressMessage("InconsistentNaming")
            + "\n    public void Method() { }\n}";

        this.TestIt(input: input, expected: expected);
    }

    [Fact]
    public void ReplaceShouldPreserveCarriageReturnLineEndings()
    {
        string input =
            "public sealed class Example\r\n{\r\n    "
            + MakeDisable("InconsistentNaming")
            + "\r\n    public void Method() { }\r\n}";

        string expected =
            "public sealed class Example\r\n{\r\n    "
            + MakeSuppressMessage("InconsistentNaming")
            + "\r\n    public void Method() { }\r\n}";

        this.TestIt(input: input, expected: expected);
    }

    private void TestIt(string input, string expected)
    {
        string result = this._resharperSuppressionToSuppressMessage.Replace(input);

        Assert.Equal(expected: expected, actual: result);
    }
}
