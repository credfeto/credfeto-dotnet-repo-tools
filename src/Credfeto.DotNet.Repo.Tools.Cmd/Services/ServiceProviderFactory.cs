using System;
using Credfeto.DotNet.Repo.Tools.Build;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Services;

public sealed class ServiceProviderFactory : IServiceProviderFactory
{
    public IServiceProvider Build(IServiceCollection serviceCollection)
    {
        return serviceCollection.BuildServiceProvider();
    }
}