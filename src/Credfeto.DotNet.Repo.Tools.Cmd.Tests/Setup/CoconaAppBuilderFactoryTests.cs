using System;
using System.Collections.Generic;
using System.Linq;
using Cocona;
using Credfeto.DotNet.Repo.Tools.Cmd.Setup;
using FunFair.Test.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Tests.Setup;

// Tests in one class share an xunit collection, so the tests that change this process-wide variable never run in parallel.
public sealed class CoconaAppBuilderFactoryTests : TestBase
{
    private const string RELOAD_CONFIG_ON_CHANGE_VARIABLE = "DOTNET_hostBuilder__reloadConfigOnChange";

    [Fact]
    public void CreatedAppDoesNotWatchConfigurationFilesForChanges()
    {
        using (CoconaApp app = CoconaAppBuilderFactory.Create([]).Build())
        {
            IConfigurationRoot configuration = (IConfigurationRoot)app.Services.GetRequiredService<IConfiguration>();

            IReadOnlyList<FileConfigurationProvider> fileProviders =
            [
                .. configuration.Providers.OfType<FileConfigurationProvider>(),
            ];

            Assert.NotEmpty(fileProviders);

            IReadOnlyList<string?> reloadingFiles =
            [
                .. fileProviders
                    .Where(provider => provider.Source.ReloadOnChange)
                    .Select(provider => provider.Source.Path),
            ];

            Assert.Empty(reloadingFiles);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    public static void CreateRestoresReloadConfigOnChangeVariable(string? valueBeforeCreate)
    {
        string? originalValue = Environment.GetEnvironmentVariable(RELOAD_CONFIG_ON_CHANGE_VARIABLE);

        try
        {
            Environment.SetEnvironmentVariable(variable: RELOAD_CONFIG_ON_CHANGE_VARIABLE, value: valueBeforeCreate);

            _ = CoconaAppBuilderFactory.Create([]);

            Assert.Equal(
                expected: valueBeforeCreate,
                Environment.GetEnvironmentVariable(RELOAD_CONFIG_ON_CHANGE_VARIABLE)
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: RELOAD_CONFIG_ON_CHANGE_VARIABLE, value: originalValue);
        }
    }
}
