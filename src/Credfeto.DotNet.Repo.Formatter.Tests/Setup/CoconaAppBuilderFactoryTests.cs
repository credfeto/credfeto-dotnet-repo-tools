using System.Collections.Generic;
using System.Linq;
using Cocona;
using Credfeto.DotNet.Repo.Formatter.Setup;
using FunFair.Test.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Formatter.Tests.Setup;

public sealed class CoconaAppBuilderFactoryTests : TestBase
{
    [Fact]
    public void CreatedAppDoesNotWatchConfigurationFilesForChanges()
    {
        using (CoconaApp app = CoconaAppBuilderFactory.Create([]).Build())
        {
            IConfigurationRoot configuration = (IConfigurationRoot)app.Services.GetRequiredService<IConfiguration>();

            IReadOnlyList<string?> reloadingFiles =
            [
                .. configuration
                    .Providers.OfType<FileConfigurationProvider>()
                    .Where(provider => provider.Source.ReloadOnChange)
                    .Select(provider => provider.Source.Path),
            ];

            Assert.Empty(reloadingFiles);
        }
    }
}
