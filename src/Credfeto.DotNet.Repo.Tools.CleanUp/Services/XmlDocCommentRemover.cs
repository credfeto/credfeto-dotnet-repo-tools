using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed partial class XmlDocCommentRemover : IXmlDocCommentRemover
{
    private const string HorizontalWhitespace = " \t";

    public string RemoveXmlDocComments(string content)
    {
        // Cheap textual pre-check so files with no doc comments are never parsed.
        if (
            !content.Contains(value: "///", comparisonType: StringComparison.Ordinal)
            && !content.Contains(value: "/**", comparisonType: StringComparison.Ordinal)
        )
        {
            return content;
        }

        IReadOnlyList<TextChange> removals =
        [
            .. CSharpSyntaxTree
                .ParseText(content)
                .GetRoot()
                .DescendantTrivia(descendIntoTrivia: false)
                .SelectMany(trivia => GetRemovals(content: content, trivia: trivia)),
        ];

        return removals is [] ? content : SourceText.From(content).WithChanges(removals).ToString();
    }

    private static IEnumerable<TextChange> GetRemovals(string content, in SyntaxTrivia trivia)
    {
        if (
            trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
            || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
        )
        {
            return [GetRemoval(content: content, commentSpan: trivia.FullSpan)];
        }

        // Roslyn leaves code in inactive #if branches as opaque text, so doc comments there are not trivia.
        return trivia.IsKind(SyntaxKind.DisabledTextTrivia)
            ? GetDisabledTextRemovals(content: content, disabledSpan: trivia.FullSpan)
            : [];
    }

    private static IEnumerable<TextChange> GetDisabledTextRemovals(string content, in TextSpan disabledSpan)
    {
        List<TextChange> removals = [];

        foreach (
            ValueMatch match in DocCommentLines()
                .EnumerateMatches(content.AsSpan(start: disabledSpan.Start, length: disabledSpan.Length))
        )
        {
            removals.Add(
                new(span: new(start: disabledSpan.Start + match.Index, length: match.Length), newText: string.Empty)
            );
        }

        return removals;
    }

    private static TextChange GetRemoval(string content, in TextSpan commentSpan)
    {
        int start = content.AsSpan(start: 0, length: commentSpan.Start).TrimEnd(HorizontalWhitespace).Length;
        int contentEnd = TrimLineBreak(content: content, commentSpan: commentSpan);
        int lineBreakStart = content.Length - content.AsSpan(contentEnd).TrimStart(HorizontalWhitespace).Length;

        if (!IsEndOfLine(content: content, position: lineBreakStart))
        {
            // Code shares the line after the comment, so only the comment and the gap before that code go.
            return ToRemoval(
                content: content,
                span: TextSpan.FromBounds(start: commentSpan.Start, end: lineBreakStart)
            );
        }

        // Taking the line break too stops a blank line being left behind, but only when nothing else was on the line.
        int end = IsLineStart(content: content, position: start)
            ? lineBreakStart + GetLineBreakLength(content: content, position: lineBreakStart)
            : lineBreakStart;

        return ToRemoval(content: content, span: TextSpan.FromBounds(start: start, end: end));
    }

    private static TextChange ToRemoval(string content, in TextSpan span)
    {
        // A comment squeezed between two tokens must not let them fuse together.
        return new(span: span, newText: IsBetweenTokens(content: content, removal: span) ? " " : string.Empty);
    }

    // Single line doc comment trivia owns its trailing line break; multi line ones do not.
    private static int TrimLineBreak(string content, in TextSpan commentSpan)
    {
        int end = commentSpan.End;

        while (end > commentSpan.Start && IsLineEnding(content[end - 1]))
        {
            --end;
        }

        return end;
    }

    private static bool IsEndOfLine(string content, int position)
    {
        return position == content.Length || IsLineEnding(content[position]);
    }

    private static int GetLineBreakLength(string content, int position)
    {
        if (position == content.Length)
        {
            return 0;
        }

        return content[position] == '\r' && position + 1 < content.Length && content[position + 1] == '\n' ? 2 : 1;
    }

    private static bool IsLineStart(string content, int position)
    {
        return position == 0 || IsLineEnding(content[position - 1]);
    }

    private static bool IsBetweenTokens(string content, in TextSpan removal)
    {
        return removal.Start > 0
            && removal.End < content.Length
            && !char.IsWhiteSpace(content[removal.Start - 1])
            && !char.IsWhiteSpace(content[removal.End]);
    }

    private static bool IsLineEnding(char character)
    {
        return character is '\r' or '\n';
    }

    [GeneratedRegex(
        pattern: "(?<![^\\r\\n])[ \\t]*///(?!/)[^\\r\\n]*(\\r\\n|\\r|\\n)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 5000
    )]
    private static partial Regex DocCommentLines();
}
