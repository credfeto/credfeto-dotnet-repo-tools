using System;
using Cocona;
using Credfeto.DotNet.Repo.Formatter.Setup;

namespace Credfeto.DotNet.Repo.Formatter;

public sealed class SelfTestCommand
{
    private readonly IServiceProvider _serviceProvider;

    public SelfTestCommand(IServiceProvider serviceProvider)
    {
        this._serviceProvider = serviceProvider;
    }

    [Command(Description = "Check the tool can start, then exit without processing any files")]
    public int SelfTest()
    {
        return StartupSelfTest.Run(serviceProvider: this._serviceProvider, errorOutput: Console.Error);
    }
}
