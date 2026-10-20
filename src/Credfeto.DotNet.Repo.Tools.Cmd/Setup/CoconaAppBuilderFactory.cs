using System;
using System.Collections.Generic;
using Cocona;
using Cocona.Builder;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Setup;

public static class CoconaAppBuilderFactory
{
    private const string RELOAD_CONFIG_ON_CHANGE_VARIABLE = "DOTNET_hostBuilder__reloadConfigOnChange";

    public static CoconaAppBuilder Create(IReadOnlyList<string> args)
    {
        // Cocona applies the host defaults inside CreateBuilder, so this setting can only be supplied through the environment.
        // Reload-on-change puts a recursive inotify watcher over the working directory which can deadlock host disposal on Linux.
        string? previousValue = Environment.GetEnvironmentVariable(RELOAD_CONFIG_ON_CHANGE_VARIABLE);
        Environment.SetEnvironmentVariable(variable: RELOAD_CONFIG_ON_CHANGE_VARIABLE, value: "false");

        try
        {
            return CoconaApp.CreateBuilder([.. args]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: RELOAD_CONFIG_ON_CHANGE_VARIABLE, value: previousValue);
        }
    }
}
