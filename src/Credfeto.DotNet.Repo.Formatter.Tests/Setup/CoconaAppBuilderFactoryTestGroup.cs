using Xunit;

namespace Credfeto.DotNet.Repo.Formatter.Tests.Setup;

// CoconaAppBuilderFactory.Create changes the process-wide DOTNET_hostBuilder__reloadConfigOnChange variable,
// so every test class that calls it or reads that variable joins this collection and never runs in parallel with another.
[CollectionDefinition(NAME)]
public sealed class CoconaAppBuilderFactoryTestGroup
{
    public const string NAME = "CoconaAppBuilderFactory";
}
