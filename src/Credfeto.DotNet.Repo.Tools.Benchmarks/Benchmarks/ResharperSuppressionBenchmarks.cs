using BenchmarkDotNet.Attributes;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;

namespace Credfeto.DotNet.Repo.Tools.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public sealed class ResharperSuppressionBenchmarks
{
    private readonly ResharperSuppressionToSuppressMessage _service = new();

    // Typical file content with several suppression comments spread across ~200 lines
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
    public string Replace() => this._service.Replace(SampleContent);
}
