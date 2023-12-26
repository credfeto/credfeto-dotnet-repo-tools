using System;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces;

public interface IServiceProviderFactory
{
    IServiceProvider Build(IServiceCollection serviceCollection);
}