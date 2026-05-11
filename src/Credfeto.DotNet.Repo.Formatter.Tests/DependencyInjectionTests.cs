using Credfeto.DotNet.Repo.Tools.Build;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Formatter.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    [Fact]
    public void ProjectXmlRewriterMustBeRegistered()
    {
        this.RequireService<IProjectXmlRewriter>();
    }

    [Fact]
    public void SourceFileReformatterMustBeRegistered()
    {
        this.RequireService<ISourceFileReformatter>();
    }

    [Fact]
    public void XmlDocCommentRemoverMustBeRegistered()
    {
        this.RequireService<IXmlDocCommentRemover>();
    }

    [Fact]
    public void ResharperSuppressionToSuppressMessageMustBeRegistered()
    {
        this.RequireService<IResharperSuppressionToSuppressMessage>();
    }

    [Fact]
    public void SourceFileSuppressionRemoverMustBeRegistered()
    {
        this.RequireService<ISourceFileSuppressionRemover>();
    }

    [Fact]
    public void DotNetBuildMustBeRegistered()
    {
        this.RequireService<IDotNetBuild>();
    }

    [Fact]
    public void DotNetFilesDetectorMustBeRegistered()
    {
        this.RequireService<IDotNetFilesDetector>();
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddBuild().AddCleanUp();
    }
}
