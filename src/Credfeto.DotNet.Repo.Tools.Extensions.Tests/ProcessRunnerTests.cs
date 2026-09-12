using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Extensions.Tests;

public sealed class ProcessRunnerTests : TestBase
{
    [Fact]
    public async Task ExecAsyncWhenProcessSucceedsReturnsOutputAndExitCodeAsync()
    {
        ProcessStartInfo psi = CreateProcessStartInfo(arguments: "-c \"echo hello-stdout\"");

        (string[] output, int exitCode) = await ProcessRunner.ExecAsync(
            psi: psi,
            failedToStartMessage: "Failed to start sh",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 0, actual: exitCode);
        Assert.Contains(expected: "hello-stdout", collection: output, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task ExecAsyncWhenProcessWritesToStandardErrorReturnsCombinedOutputAndNonZeroExitCodeAsync()
    {
        ProcessStartInfo psi = CreateProcessStartInfo(arguments: "-c \"echo failure-stderr 1>&2; exit 7\"");

        (string[] output, int exitCode) = await ProcessRunner.ExecAsync(
            psi: psi,
            failedToStartMessage: "Failed to start sh",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 7, actual: exitCode);
        Assert.Contains(expected: "failure-stderr", collection: output, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task ExecAsyncWhenCancelledKillsProcessAndThrowsAsync()
    {
        ProcessStartInfo psi = CreateProcessStartInfo(arguments: "-c \"sleep 30\"");

        using CancellationTokenSource cancellationTokenSource = new();

        ValueTask<(string[] Output, int ExitCode)> task = ProcessRunner.ExecAsync(
            psi: psi,
            failedToStartMessage: "Failed to start sh",
            cancellationToken: cancellationTokenSource.Token
        );

        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
    }

    [SuppressMessage(
        category: "SonarAnalyzer.CSharp",
        checkId: "S4036: Use an absolute path for this command",
        Justification = "Relies on sh being resolved via PATH"
    )]
    private static ProcessStartInfo CreateProcessStartInfo(string arguments)
    {
        return new()
        {
            FileName = "sh",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }
}
