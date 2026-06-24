using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed class ResharperSuppressionToSuppressMessage : IResharperSuppressionToSuppressMessage
{
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
        pattern: "//\\s+ReSharper\\s+disable\\s+once\\s+(?<Rule>"
            + string.Join(separator: '|', Replacements.Select(Regex.Escape))
            + ")",
        options: RegexOptions.Compiled
            | RegexOptions.CultureInvariant
            | RegexOptions.NonBacktracking
            | RegexOptions.ExplicitCapture,
        matchTimeout: TimeSpan.FromSeconds(1)
    );

    public string Replace(string content)
    {
        return CombinedRegex.Replace(
            input: content,
            evaluator: m =>
                ReplacementMap.TryGetValue(key: m.Groups["Rule"].Value, out string? replacement) ? replacement : m.Value
        );
    }
}
