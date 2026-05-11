using BenchmarkDotNet.Running;
using Credfeto.DotNet.Repo.Tools.Benchmarks.Benchmarks;

BenchmarkRunner.Run<ResharperSuppressionBenchmarks>();
BenchmarkRunner.Run<ProjectXmlRewriterBenchmarks>();
