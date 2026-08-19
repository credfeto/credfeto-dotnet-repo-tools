using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Credfeto.DotNet.Repo.Tools.CleanUp;

public static class RoslynSyntaxValidation
{
    public static int CountSyntaxErrors(string content, in CancellationToken cancellationToken)
    {
        return CountSyntaxErrors(
            tree: Parse(content: content, cancellationToken: cancellationToken),
            cancellationToken: cancellationToken
        );
    }

    // Two independent checks: a bare syntax-tree parse (noise-free, no assembly references needed)
    // catches genuine parse errors such as an attribute list before a case label or a collection
    // element; the compilation-level diagnostic count catches attribute lists that parse fine but
    // are semantically illegal where they now sit (e.g. before a local declaration or statement).
    public static (int SyntaxErrors, int TargetedErrors) CountErrors(
        string content,
        string diagnosticId,
        in CancellationToken cancellationToken
    )
    {
        SyntaxTree tree = Parse(content: content, cancellationToken: cancellationToken);

        return (
            CountSyntaxErrors(tree: tree, cancellationToken: cancellationToken),
            CountCompilationErrors(tree: tree, diagnosticId: diagnosticId, cancellationToken: cancellationToken)
        );
    }

    private static SyntaxTree Parse(string content, in CancellationToken cancellationToken)
    {
        return CSharpSyntaxTree.ParseText(text: content, cancellationToken: cancellationToken);
    }

    private static int CountSyntaxErrors(SyntaxTree tree, in CancellationToken cancellationToken)
    {
        return tree.GetDiagnostics(cancellationToken: cancellationToken)
            .Count(d => d.Severity == DiagnosticSeverity.Error);
    }

    private static int CountCompilationErrors(
        SyntaxTree tree,
        string diagnosticId,
        in CancellationToken cancellationToken
    )
    {
        return CSharpCompilation
            .Create(assemblyName: "SyntaxValidation")
            .AddSyntaxTrees(tree)
            .GetDiagnostics(cancellationToken: cancellationToken)
            .Count(d => d.Severity == DiagnosticSeverity.Error && StringComparer.Ordinal.Equals(d.Id, diagnosticId));
    }
}
