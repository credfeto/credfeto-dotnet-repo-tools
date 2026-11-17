using System;
using System.IO;
using Credfeto.DotNet.Repo.Formatter.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.DotNet.Repo.Formatter.Setup;

public static class StartupSelfTest
{
    public static int Run(IServiceProvider serviceProvider, TextWriter errorOutput)
    {
        try
        {
            // Activating the command resolves its whole dependency graph without running any of it.
            _ = ActivatorUtilities.CreateInstance<Commands>(serviceProvider);

            return ExitCodes.Success;
        }
        catch (Exception exception)
        {
            errorOutput.WriteLine($"Self-test failed: {ToSingleLine(exception.Message)}");

            return ExitCodes.Error;
        }
    }

    private static string ToSingleLine(string message)
    {
        return message.ReplaceLineEndings(" ");
    }
}
