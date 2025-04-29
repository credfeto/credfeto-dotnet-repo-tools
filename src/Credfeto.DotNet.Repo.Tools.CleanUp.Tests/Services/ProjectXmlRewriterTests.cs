using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

[SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "Unit tests")]
public sealed partial class ProjectXmlRewriterTests : LoggingTestBase
{
    private readonly IProjectXmlRewriter _projectXmlRewriter;

    public ProjectXmlRewriterTests(ITestOutputHelper output)
        : base(output)
    {
        this._projectXmlRewriter = new ProjectXmlRewriter(this.GetTypedLogger<ProjectXmlRewriter>());
    }

    private async Task DoReOrderPropertiesAsync(string expectedXml, string originalXml)
    {
        string txtExpected = await GetValidatedExpectedDocumentAsync(expectedXml);

        XmlDocument doc = LoadXml(originalXml);

        this._projectXmlRewriter.ReOrderPropertyGroups(projectDocument: doc, filename: "test.csproj");

        await this.DoComparaisonAsync(doc: doc, txtExpected: txtExpected);
    }

    private static ValueTask<string> GetValidatedExpectedDocumentAsync(string expectedXml)
    {
        XmlDocument docExpected = LoadXml(expectedXml);

        return SaveDocAsync(docExpected);
    }

    private async ValueTask DoComparaisonAsync(XmlDocument doc, string txtExpected)
    {
        string actual = await SaveDocAsync(doc);
        this.Output.WriteLine(">>>>>> ACTUAL <<<<<<");
        this.Output.WriteLine(actual);
        this.Output.WriteLine(">>>>> EXPECTED <<<<<");
        this.Output.WriteLine(txtExpected);
        Assert.Equal(expected: txtExpected, actual: actual);
    }

    private async Task DoReOrderItemGroupsAsync(string expectedXml, string originalXml)
    {
        string txtExpected = await GetValidatedExpectedDocumentAsync(expectedXml);

        XmlDocument doc = LoadXml(originalXml);

        this._projectXmlRewriter.ReOrderIncludes(projectDocument: doc, filename: "test.csproj");

        await this.DoComparaisonAsync(doc: doc, txtExpected: txtExpected);
    }

    private static XmlDocument LoadXml(string expectedXml)
    {
        XmlDocument docExpected = new();
        docExpected.LoadXml(expectedXml);

        return docExpected;
    }

    private static async ValueTask<string> SaveDocAsync(XmlDocument doc)
    {
        XmlWriterSettings settings = new()
        {
            Indent = true,
            IndentChars = "  ",
            NewLineOnAttributes = false,
            OmitXmlDeclaration = true,
            Async = true,
        };

        StringWriter sw = new();

        // ReSharper disable once UseAwaitUsing
        await using (XmlWriter xmlWriter = XmlWriter.Create(output: sw, settings: settings))
        {
            doc.Save(xmlWriter);
            await ValueTask.CompletedTask;
        }

        return sw.ToString();
    }
}
