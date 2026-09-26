using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed class XmlDocCommentRemover : IXmlDocCommentRemover
{
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

        IReadOnlyList<TextSpan> removals =
        [
            .. CSharpSyntaxTree
                .ParseText(content)
                .GetRoot()
                .DescendantTrivia(descendIntoTrivia: false)
                .Where(IsXmlDocComment)
                .Select(trivia => GetRemovalSpan(content: content, commentSpan: trivia.FullSpan)),
        ];

        return removals is [] ? content : ApplyRemovals(content: content, removals: removals);
    }

    private static bool IsXmlDocComment(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
            || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
    }

    private static string ApplyRemovals(string content, IReadOnlyList<TextSpan> removals)
    {
        StringBuilder result = new(content.Length);
        int position = 0;

        foreach (TextSpan removal in removals)
        {
            result.Append(value: content, startIndex: position, count: removal.Start - position);

            // A comment squeezed between two tokens must not let them fuse together.
            if (IsBetweenTokens(content: content, removal: removal))
            {
                result.Append(' ');
            }

            position = removal.End;
        }

        result.Append(value: content, startIndex: position, count: content.Length - position);

        return result.ToString();
    }

    private static TextSpan GetRemovalSpan(string content, in TextSpan commentSpan)
    {
        int start = SkipHorizontalWhitespaceBackwards(content: content, position: commentSpan.Start);
        int contentEnd = TrimLineBreak(content: content, commentSpan: commentSpan);
        int lineBreakStart = SkipHorizontalWhitespaceForwards(content: content, position: contentEnd);

        if (!IsEndOfLine(content: content, position: lineBreakStart))
        {
            // Code shares the line after the comment, so only the comment itself goes.
            return commentSpan;
        }

        // Taking the line break too stops a blank line being left behind, but only when nothing else was on the line.
        return IsLineStart(content: content, position: start)
            ? TextSpan.FromBounds(
                start: start,
                end: lineBreakStart + GetLineBreakLength(content: content, position: lineBreakStart)
            )
            : TextSpan.FromBounds(start: start, end: lineBreakStart);
    }

    // Single line doc comment trivia owns its trailing line break; multi line ones do not.
    private static int TrimLineBreak(string content, in TextSpan commentSpan)
    {
        int end = commentSpan.End;

        if (end > commentSpan.Start && content[end - 1] == '\n')
        {
            --end;
        }

        if (end > commentSpan.Start && content[end - 1] == '\r')
        {
            --end;
        }

        return end;
    }

    private static int SkipHorizontalWhitespaceForwards(string content, int position)
    {
        while (position < content.Length && IsHorizontalWhitespace(content[position]))
        {
            ++position;
        }

        return position;
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

    private static int SkipHorizontalWhitespaceBackwards(string content, int position)
    {
        while (position > 0 && IsHorizontalWhitespace(content[position - 1]))
        {
            --position;
        }

        return position;
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

    private static bool IsHorizontalWhitespace(char character)
    {
        return character is ' ' or '\t';
    }
}
