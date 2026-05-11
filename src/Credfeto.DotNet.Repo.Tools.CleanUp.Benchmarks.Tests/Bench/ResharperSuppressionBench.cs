using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Benchmarks.Tests.Bench;

[SimpleJob]
[MemoryDiagnoser(false)]
[SuppressMessage(category: "codecracker.CSharp", checkId: "CC0091:MarkMembersAsStatic", Justification = "Benchmark")]
[SuppressMessage(
    category: "FunFair.CodeAnalysis",
    checkId: "FFS0012: Make sealed static or abstract",
    Justification = "Benchmark"
)]
public class ResharperSuppressionBench
{
    private readonly ResharperSuppressionToSuppressMessage _service = new();

    private const string SampleContent = """
        using System;
        using System.Collections.Generic;

        namespace Example;

        // ReSharper disable once ClassNeverInstantiated.Global
        public sealed class MyService
        {
            // ReSharper disable once UnusedMember.Global
            public void DoNothing() { }

            // ReSharper disable once MemberCanBePrivate.Global
            public string Name { get; set; } = string.Empty;

            // ReSharper disable once InconsistentNaming
            private int _myField;

            // ReSharper disable once UnusedType.Local
            private sealed class Inner { }

            // ReSharper disable once HeapView.BoxingAllocation
            public object Box(int x) => x;

            // ReSharper disable once RedundantDefaultMemberInitializer
            private bool _flag = false;
        }
        """;

    [Benchmark]
    public string Replace()
    {
        return this._service.Replace(SampleContent);
    }
}
