using System;
using System.Threading.Tasks;
using Cocona;
using Cocona.Builder;
using Credfeto.DotNet.Repo.Formatter.Constants;
using Credfeto.DotNet.Repo.Formatter.Setup;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Formatter.Tests;

// Tests in one class share an xunit collection, so the tests that read the process-wide exit code never run in parallel.
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
