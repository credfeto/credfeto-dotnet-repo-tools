using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using Credfeto.DotNet.Repo.Tools.CleanUp.Benchmarks.Tests.Bench;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Benchmarks.Tests;

public sealed class ProjectXmlRewriterBenchmarkTests : LoggingTestBase
{
    public ProjectXmlRewriterBenchmarkTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public void Run_Benchmarks()
    {
        (Summary _, AccumulationLogger logger) = Benchmark<ProjectXmlRewriterBench>();

        this.Output.WriteLine(logger.GetLog());
    }
}
