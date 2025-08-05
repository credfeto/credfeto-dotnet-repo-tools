using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Credfeto.Tsql.Formatter;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure)
    {
    }

    [Fact]
    public void CleanUpBuildMustBeRegistered()
    {
        this.RequireService<IBulkCodeCleanUp>();
    }

    [Fact]
    public void TransactSqlFormatterMustBeRegistered()
    {
        this.RequireService<ITransactSqlFormatter>();
    }

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

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddMockedService<ITrackingCache>()
                       .AddMockedService<IGitRepositoryFactory>()
                       .AddMockedService<IGlobalJson>()
                       .AddMockedService<IReleaseConfigLoader>()
                       .AddMockedService<IDotNetVersion>()
                       .AddMockedService<IDotNetBuild>()
                       .AddMockedService<IProjectFinder>()
                       .AddCleanUp();
    }
}