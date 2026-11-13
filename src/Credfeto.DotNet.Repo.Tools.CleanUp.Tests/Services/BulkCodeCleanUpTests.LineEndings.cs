using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using LibGit2Sharp;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

public sealed partial class BulkCodeCleanUpTests
{
    private async Task<string> RunBulkCleanupOnFileAsync(string repoName, string fileName, string content)
    {
        (Repository activeRepo, string projectFile, IGitRepository testRepo) =
            await this.SetupRepoDirWithChangelogAndProjectAsync(
                repoDirName: repoName,
                cloneUrl: "https://github.com/test/" + repoName + ".git",
                headRev: repoName
            );

        using (activeRepo)
        {
            string repoDir = testRepo.WorkingDirectory;
            string file = Path.Combine(path1: repoDir, path2: fileName);
            await File.WriteAllTextAsync(path: file, contents: content, cancellationToken: this.CancellationToken());

            this.SetupDotNetFilesAndBuild(repoDir: repoDir, projectFile: projectFile);

            IGitRepository templateRepo = GetSubstitute<IGitRepository>();
            this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

            await this.RunBulkUpdateAsync("https://github.com/test/" + repoName + ".git");

            return await File.ReadAllTextAsync(path: file, cancellationToken: this.CancellationToken());
        }
    }

    [SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2012: Use ValueTasks correctly",
        Justification = "NSubstitute mock setup requires calling async methods without awaiting"
    )]
    private static void MockSourceFileReformatterReformat(
        ISourceFileReformatter reformatter,
        string original,
        string reformatted
    )
    {
        reformatter
            .ReformatAsync(Arg.Any<string>(), original, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(reformatted));
    }

    [Theory]
    [InlineData("public class Foo\n{\n}\n", "public class Foo { }\r\n", "public class Foo { }\n")]
    [InlineData("public class Foo\r\n{\r\n}\r\n", "public class Foo { }\n", "public class Foo { }\r\n")]
    public async Task BulkUpdateAsyncKeepsLineEndingsOfReformattedCSharpFileAsync(
        string original,
        string reformatted,
        string expected
    )
    {
        this.SetupPassThroughCsCleaners();
        MockSourceFileReformatterReformat(
            reformatter: this._sourceFileReformatter,
            original: original,
            reformatted: reformatted
        );

        string result = await this.RunBulkCleanupOnFileAsync(
            repoName: "gitrepo-cs-line-endings",
            fileName: "Foo.cs",
            content: original
        );

        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task BulkUpdateAsyncKeepsLineEndingsOfRewrittenSqlFileAsync(string newLine)
    {
        this.SetupPassThroughCsCleaners();
        this._tsqlFormatter.FormatAsync(
                Arg.Any<string>(),
                Arg.Any<SqlScriptGeneratorOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns("SELECT 1;\r\nSELECT 2;\n");

        string result = await this.RunBulkCleanupOnFileAsync(
            repoName: "gitrepo-sql-line-endings",
            fileName: "schema.sql",
            content: "select 1;" + newLine + "select 2;" + newLine
        );

        Assert.Equal(expected: "SELECT 1;" + newLine + "SELECT 2;" + newLine, actual: result);
    }

    [Fact]
    public async Task BulkUpdateAsyncKeepsCrLfLineEndingsOfRewrittenProjectFileAsync()
    {
        const string original =
            "<Project Sdk=\"Microsoft.NET.Sdk\">\r\n  <ItemGroup>\r\n    <Compile Include=\"a.cs\" />\r\n  </ItemGroup>\r\n</Project>\r\n";
        this.SetupPassThroughCsCleaners();
        this._projectXmlRewriter.ReOrderIncludes(Arg.Any<XmlDocument>(), Arg.Any<string>()).Returns(true);

        string result = await this.RunBulkCleanupOnFileAsync(
            repoName: "gitrepo-csproj-line-endings",
            fileName: "Test.csproj",
            content: original
        );

        Assert.Equal(expected: original, actual: result);
    }
}
