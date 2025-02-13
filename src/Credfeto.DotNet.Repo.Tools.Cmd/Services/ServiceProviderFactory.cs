using System;
using System.Diagnostics.CodeAnalysis;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Services;

public sealed class ServiceProviderFactory : IServiceProviderFactory
{
    [SuppressMessage(
        category: "ReSharper",
        checkId: "UnusedMember.Global",
        Justification = "False positive - the interface defines the method"
    )]
    public IServiceProvider Build(IServiceCollection serviceCollection)
    {
        return serviceCollection.BuildServiceProvider();
    }
}
