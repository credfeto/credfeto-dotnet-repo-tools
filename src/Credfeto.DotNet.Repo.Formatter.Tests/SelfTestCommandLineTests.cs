using System;
using System.Threading.Tasks;
using Cocona;
using Cocona.Builder;
using Credfeto.DotNet.Repo.Formatter.Constants;
using Credfeto.DotNet.Repo.Formatter.Setup;
using Credfeto.DotNet.Repo.Formatter.Tests.Setup;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Formatter.Tests;

// Shares an xunit collection with every other class that calls CoconaAppBuilderFactory.Create, so these tests, which read the process-wide exit code
// and run Create, which changes a process-wide environment variable, never run in parallel with each other or with those classes.
[Collection(CoconaAppBuilderFactoryTestGroup.NAME)]
public sealed class SelfTestCommandLineTests : TestBase
{
    private const int EXIT_CODE_NOT_SET = 42;

    [Fact]
    public Task SelfTestSwitchExitsSuccessfullyWithoutInputsAsync()
    {
        return this.AssertSelfTestExitCodeAsync(registerServices: true, expectedExitCode: ExitCodes.Success);
    }

    [Fact]
    public Task SelfTestSwitchExitsWithErrorWhenStartupFailsAsync()
    {
        return this.AssertSelfTestExitCodeAsync(registerServices: false, expectedExitCode: ExitCodes.Error);
    }

    private async Task AssertSelfTestExitCodeAsync(bool registerServices, int expectedExitCode)
    {
        int originalExitCode = Environment.ExitCode;

        try
        {
            Environment.ExitCode = EXIT_CODE_NOT_SET;

            CoconaAppBuilder builder = CoconaAppBuilderFactory.Create(["--self-test"]);

            if (registerServices)
            {
                builder.Services.AddServices();
            }

            using (CoconaApp app = builder.Build())
            {
                app.AddCommands<Commands>();

                await app.RunAsync(this.CancellationToken());
            }

            Assert.Equal(expected: expectedExitCode, actual: Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }
    }
}
