using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Benchmarks.Tests.Bench;

[SimpleJob]
[MemoryDiagnoser(false)]

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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification="TODO: Review")]
        public sealed class MyService
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Global", Justification="TODO: Review")]
            public void DoNothing() { }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification="TODO: Review")]
            public string Name { get; set; } = string.Empty;

            [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification="TODO: Review")]
            private int _myField;

            [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Local", Justification="TODO: Review")]
            private sealed class Inner { }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "HeapView.BoxingAllocation", Justification="TODO: Review")]
            public object Box(int x) => x;

            [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer", Justification="TODO: Review")]
            private bool _flag = false;
        }
        """;

    [Benchmark]
    public string Replace()
    {
        return this._service.Replace(SampleContent);
    }
}
