using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.Extensions.Linq;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Services;

public sealed class DotNetVersion : IDotNetVersion
{
    private readonly IDotNetCommandRunner _commandRunner;

    public DotNetVersion()
    {
        this._commandRunner = new DotNetCommandRunner();
    }

    internal DotNetVersion(IDotNetCommandRunner commandRunner)
    {
        this._commandRunner = commandRunner;
    }

    public async ValueTask<IReadOnlyList<Version>> GetInstalledSdksAsync(CancellationToken cancellationToken)
    {
        (string[] output, int exitCode) = await this._commandRunner.RunAsync(
            arguments: "--list-sdks",
            cancellationToken: cancellationToken
        );

        if (exitCode != 0)
        {
            return CouldNotListInstalledSdks();
        }

        return [.. output.Select(ExtractVersion).RemoveNulls().OrderDescending()];
    }

    [DoesNotReturn]
    private static IReadOnlyList<Version> CouldNotListInstalledSdks()
    {
        throw new InvalidOperationException("Failed to list installed SDKs");
    }

    private static Version? ExtractVersion(string line)
    {
        string[] parts = line.Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return null;
        }

        return Version.TryParse(parts[0], out Version? version) ? version : null;
    }
}
