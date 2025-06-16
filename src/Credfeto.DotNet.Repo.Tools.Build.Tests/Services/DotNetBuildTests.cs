using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Services;
using FunFair.BuildCheck.Interfaces;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Build.Tests.Services;

public sealed class DotNetBuildTests : TestBase
{
    private readonly IDotNetBuild _dotNetBuild;
    private readonly IProjectXmlLoader _projectXmlLoader;

    public DotNetBuildTests()
    {
        this._projectXmlLoader = GetSubstitute<IProjectXmlLoader>();
        this._dotNetBuild = new DotNetBuild(projectLoader: this._projectXmlLoader, this.GetTypedLogger<DotNetBuild>());
    }

    [Theory]
    [InlineData(nameof(Projects.PackableLibrary), true, false, null)]
    [InlineData(nameof(Projects.PublishableExe), false, true, "net9.0")]
    [InlineData(nameof(Projects.PackablePublishableDotNetTool), true, true, "net9.0")]
    [InlineData(nameof(Projects.NonPackableLibrary), false, false, null)]
    [InlineData(nameof(Projects.NonPublishableExe), false, false, null)]
    public async Task LoadBuildSettingsAsync(string project, bool packable, bool publishable, string? framework)
    {
        this.MockLoadProject(path: project);

        BuildSettings result = await this._dotNetBuild.LoadBuildSettingsAsync([project], cancellationToken: System.Threading.CancellationToken.None);

        Assert.Equal(expected: packable, actual: result.Packable);
        Assert.Equal(expected: publishable, actual: result.Publishable);

        if (framework is null)
        {
            Assert.Null(result.Framework);
        }
        else
        {
            Assert.Equal(framework, result.Framework);
        }
    }

    [Fact]
    public async Task NoProjectsAsync()
    {
        BuildSettings result = await this._dotNetBuild.LoadBuildSettingsAsync([], cancellationToken: System.Threading.CancellationToken.None);

        Assert.False(condition: result.Packable, userMessage: "Empty solutions should not be packable");
        Assert.False(condition: result.Publishable, userMessage: "Empty solutions should not be publishable");
        Assert.Null(result.Framework);
    }

    private void MockLoadProject(string path)
    {
        this._projectXmlLoader.LoadAsync(path: path, Arg.Any<CancellationToken>())
            .Returns(LoadDoc());

        XmlDocument LoadDoc()
        {
            string content = GetContent(path);

            XmlDocument doc = new();
            doc.LoadXml(content);

            return doc;
        }
    }

    private static string GetContent(string path)
    {
        string content = path switch
        {
            nameof(Projects.PackableLibrary) => Projects.PackableLibrary,
            nameof(Projects.PackablePublishableDotNetTool) => Projects.PackablePublishableDotNetTool,
            nameof(Projects.PublishableExe) => Projects.PublishableExe,
            nameof(Projects.NonPackableLibrary) => Projects.NonPackableLibrary,
            nameof(Projects.NonPublishableExe) => Projects.NonPublishableExe,
            _ => throw new FileNotFoundException(message: "No such file", fileName: path),
        };

        return content;
    }

    private static class Projects
    {
        public const string PackableLibrary =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><IsPackable>true</IsPackable><IsPublishable>false</IsPublishable><OutputType>Library</OutputType><PackAsTool>false</PackAsTool></PropertyGroup></Project>";

        public const string PackablePublishableDotNetTool =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><IsPackable>true</IsPackable><IsPublishable>true</IsPublishable><OutputType>Exe</OutputType><PackAsTool>true</PackAsTool><TargetFrameworks>net8.0;net9.0</TargetFrameworks></PropertyGroup></Project>";

        public const string NonPackableLibrary =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><IsPackable>false</IsPackable><IsPublishable>false</IsPublishable><OutputType>Library</OutputType><PackAsTool>false</PackAsTool></PropertyGroup></Project>";

        public const string PublishableExe =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><IsPackable>false</IsPackable><IsPublishable>true</IsPublishable><OutputType>Exe</OutputType><PackAsTool>false</PackAsTool><TargetFrameworks>net8.0;net9.0</TargetFrameworks></PropertyGroup></Project>";

        public const string NonPublishableExe =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><IsPackable>false</IsPackable><IsPublishable>false</IsPublishable><OutputType>Exe</OutputType><PackAsTool>false</PackAsTool><TargetFrameworks>net8.0;net9.0</TargetFrameworks></PropertyGroup></Project>";
    }
}