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
        return "// Resharper disable once " + item;
    }

    [Fact]
    public void Replace1()
    {
        string input = MakeDisable("InconsistentNaming") + @"
            public void Example()
            {
                // Simple
            }
";

        const string expected = @"[System.Diagnostics.CodeAnalysis.SuppressMessage(""ReSharper"", ""InconsistentNaming"", Justification=""TODO: Review"")]
            public void Example()
            {
                // Simple
            }
";

        this.TestIt(input: input, expected: expected);
    }

    private void TestIt(string input, string expected)
    {
        string result = this._resharperSuppressionToSuppressMessage.Replace(input);

        Assert.Equal(expected: expected, actual: result);
    }
}