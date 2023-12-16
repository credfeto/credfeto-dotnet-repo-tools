using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Dotnet.Repo.Git.Tests;

public sealed class GitUtilsTests : TestBase
{
    [Theory]
    [InlineData("https://github.com/credfeto/credfeto-dotnet-repo-tools", "credfeto-dotnet-repo-tools")]
    [InlineData("git@github.com:credfeto/credfeto-dotnet-repo-tools.git", "credfeto-dotnet-repo-tools")]
    public void GetFolderForRepo(string gitUrl, string expected)
    {
        string actual = GitUtils.GetFolderForRepo(gitUrl);

        Assert.Equal(expected: expected, actual: actual);
    }
}