using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed class ResharperSuppressionToSuppressMessage : IResharperSuppressionToSuppressMessage
{
    // CS7014 "Attributes are not valid in this context" is what the compiler reports for an attribute
    // placed before a local declaration or statement; it is a binder diagnostic, not a parser one, so
    // a bare syntax-tree parse (no diagnostics) cannot detect it - a compilation is required.
    private const string ATTRIBUTE_NOT_VALID_IN_CONTEXT_DIAGNOSTIC_ID = "CS7014";

    private static readonly IReadOnlyList<string> Replacements =
    [
        "RedundantDefaultMemberInitializer",
        "ParameterOnlyUsedForPreconditionCheck.Global",
        "ParameterOnlyUsedForPreconditionCheck.Local",
        "UnusedMember.Global",
        "UnusedMember.Local",
        "AutoPropertyCanBeMadeGetOnly.Global",
        "AutoPropertyCanBeMadeGetOnly.Local",
        "ClassNeverInstantiated.Local",
        "ClassNeverInstantiated.Global",
        "ClassCanBeSealed.Global",
        "ClassCanBeSealed.Local",
        "UnusedAutoPropertyAccessor.Global",
        "UnusedAutoPropertyAccessor.Local",
        "MemberCanBePrivate.Global",
        "MemberCanBePrivate.Local",
        "InconsistentNaming",
        "IdentifierTypo",
        "UnusedTypeParameter",
        "HeapView.BoxingAllocation",
        "UnusedType.Local",
        "UnusedType.Global",
        "PrivateFieldCanBeConvertedToLocalVariable",
    ];

    private static readonly FrozenDictionary<string, string> ReplacementMap = Replacements.ToFrozenDictionary(
        keySelector: r => r,
        elementSelector: r =>
            "[System.Diagnostics.CodeAnalysis.SuppressMessage(\"ReSharper\", \""
            + r
            + "\", Justification=\"TODO: Review\")]",
        comparer: StringComparer.Ordinal
    );

    private static readonly Regex CombinedRegex = new(
        pattern: "^(?<Indent>[ \t]*)//\\s+ReSharper\\s+disable\\s+once\\s+(?<Rule>"
            + string.Join(separator: '|', Replacements.Select(Regex.Escape))
            + ")[ \t]*(?<LineEnd>\\r?)$",
        options: RegexOptions.Compiled
            | RegexOptions.CultureInvariant
            | RegexOptions.NonBacktracking
            | RegexOptions.ExplicitCapture
            | RegexOptions.Multiline,
        matchTimeout: TimeSpan.FromSeconds(1)
    );

    public string Replace(string content)
    {
        MatchCollection matches = CombinedRegex.Matches(content);

        if (matches.Count == 0)
        {
            return content;
        }

        (int baselineSyntaxErrors, int baselineAttributeContextErrors) = RoslynSyntaxValidation.CountErrors(
            content: content,
            diagnosticId: ATTRIBUTE_NOT_VALID_IN_CONTEXT_DIAGNOSTIC_ID,
            cancellationToken: CancellationToken.None
        );
        string working = content;

        // Process from the last match to the first so that earlier matches' offsets, taken
        // from the original content, stay valid as later ones are replaced.
        for (int i = matches.Count - 1; i >= 0; --i)
        {
            Match match = matches[i];

            if (!ReplacementMap.TryGetValue(key: match.Groups["Rule"].Value, out string? replacement))
            {
                continue;
            }

            string candidateReplacement = match.Groups["Indent"].Value + replacement + match.Groups["LineEnd"].Value;
            string candidate = working[..match.Index] + candidateReplacement + working[(match.Index + match.Length)..];

            (int candidateSyntaxErrors, int candidateAttributeContextErrors) = RoslynSyntaxValidation.CountErrors(
                content: candidate,
                diagnosticId: ATTRIBUTE_NOT_VALID_IN_CONTEXT_DIAGNOSTIC_ID,
                cancellationToken: CancellationToken.None
            );

            if (
                candidateSyntaxErrors <= baselineSyntaxErrors
                && candidateAttributeContextErrors <= baselineAttributeContextErrors
            )
            {
                working = candidate;
            }
        }

        return working;
    }
}
