﻿using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure)
    {
    }

    [Fact]
    public void CleanUpBuildMustBeRegistered()
    {
        this.RequireService<IBulkCodeCleanUp>();
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddCleanUp();
    }
}