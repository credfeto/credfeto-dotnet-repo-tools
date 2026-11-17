using System;
using System.IO;
using Cocona;
using Cocona.Builder;
using Credfeto.DotNet.Repo.Formatter.Constants;
using Credfeto.DotNet.Repo.Formatter.Setup;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Formatter.Tests.Setup;

public sealed class StartupSelfTestTests : TestBase
{
    [Fact]
    public void SelfTestSucceedsWhenAllDependenciesResolve()
    {
        CoconaAppBuilder builder = CoconaAppBuilderFactory.Create([]);
        builder.Services.AddServices();

        using (CoconaApp app = builder.Build())
        using (StringWriter errorOutput = new())
        {
            int exitCode = StartupSelfTest.Run(serviceProvider: app.Services, errorOutput: errorOutput);

            Assert.Equal(expected: ExitCodes.Success, actual: exitCode);
            Assert.Empty(errorOutput.ToString());
        }
    }

    [Fact]
    public void SelfTestFailsWithOneLineReasonWhenADependencyCannotBeResolved()
    {
        using (ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider())
        using (StringWriter errorOutput = new())
        {
            int exitCode = StartupSelfTest.Run(serviceProvider: serviceProvider, errorOutput: errorOutput);

            Assert.Equal(expected: ExitCodes.Error, actual: exitCode);

            string[] lines = errorOutput.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            string line = Assert.Single(lines);
            Assert.StartsWith(
                expectedStartString: "Self-test failed: ",
                actualString: line,
                comparisonType: StringComparison.Ordinal
            );
        }
    }
}
