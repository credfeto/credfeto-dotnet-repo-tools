using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using FunFair.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

public sealed class DependaBotConfigBuilderTests : LoggingTestBase
{
    private readonly IDependaBotConfigBuilder _dependaBotConfigBuilder;

    public DependaBotConfigBuilderTests(ITestOutputHelper output)
        : base(output)
    {
        this._dependaBotConfigBuilder = new DependaBotConfigBuilder(
            this.GetTypedLogger<DependaBotConfigBuilder>()
        );
    }

    [Fact]
    public void PlaceHolder()
    {
        Assert.NotNull(this._dependaBotConfigBuilder);
    }
}
