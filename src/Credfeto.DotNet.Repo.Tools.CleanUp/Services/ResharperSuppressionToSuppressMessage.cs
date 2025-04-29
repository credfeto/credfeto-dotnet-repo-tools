using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed class ResharperSuppressionToSuppressMessage : IResharperSuppressionToSuppressMessage
{
    private static readonly IReadOnlyCollection<string> Replacements =
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
        "PrivateFieldCanBeConvertedToLocalVariable"
    ];

    // private const string linesToRemoveRegex = "(?<LinesToRemove>((\r\n){2,}))";
    // private const string suppressMessageRegex = "(?<End>\\s+\\[(System\\.Diagnostics\\.CodeAnalysis\\.)?SuppressMessage)";
    // private const string removeBlankLinesRegex = "(?ms)" + "(?<Start>(^((\\s+)///\\s+</(.*?)\\>)))" + linesToRemoveRegex + suppressMessageRegex;
    //
    // private const string removeBlankLines2Regex = "(?ms)" + "(?<Start>(^((\\s+)///\\s+<(.*?)/\\>)))" + linesToRemoveRegex + suppressMessageRegex;

    private const RegexOptions REGEX_OPTIONS = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    private static readonly TimeSpan RegexTimeSpan = TimeSpan.FromSeconds(1);

    public string Replace(string content)
    {
        string source = content;

        foreach (string replacement in Replacements)
        {
            string code = replacement.Replace(oldValue: ".", newValue: "\\.", comparisonType: StringComparison.Ordinal);

            string regex = "//\\s+ReSharper\\s+disable\\s+once\\s+" + code;
            string replacementText = "[System.Diagnostics.CodeAnalysis.SuppressMessage(\"ReSharper\", \"" + replacement + "\", Justification=\"TODO: Review\")]";

            source = Regex.Replace(input: source, pattern: regex, replacement: replacementText, options: REGEX_OPTIONS, matchTimeout: RegexTimeSpan);
        }

        return source;
    }

#if FALSE
function Resharper_ConvertSuppressionCommentToSuppressMessage {
param (
    [string] $sourceFolder
    )

    Write-Information "* Changing Resharper disable once comments to SuppressMessage"
    Write-Information "  - Folder: $sourceFolder"

    $emptyLine = [char]13 + [char]10

    $linesToRemoveRegex = "(?<LinesToRemove>((\r\n){2,}))"
    $suppressMessageRegex = "(?<End>\s+\[(System\.Diagnostics\.CodeAnalysis\.)?SuppressMessage)"
    $removeBlankLinesRegex = "(?ms)" +  "(?<Start>(^((\s+)///\s+</(.*?)\>)))" + $linesToRemoveRegex + $suppressMessageRegex
    $removeBlankLines2Regex = "(?ms)" + "(?<Start>(^((\s+)///\s+<(.*?)/\>)))" + $linesToRemoveRegex + $suppressMessageRegex


    $files = Get-ChildItem -Path $sourceFolder -Filter "*.cs" -Recurse
    ForEach($file in $files) {
        $fileName = $file.FullName

        $content = Get-Content -Path $fileName -Raw
        $originalContent = $content
        $updatedContent = $content

        $changedFile = $False

        ForEach($replacement in $replacements) {
            $code = $replacement.Replace(".", "\.")
            $regex = "//\s+ReSharper\s+disable\s+once\s+$code"
            $replacementText = "[System.Diagnostics.CodeAnalysis.SuppressMessage(""ReSharper"", ""$replacement"", Justification=""TODO: Review"")]"

            $updatedContent = $content -replace $regex, $replacementText
            if($content -ne $updatedContent)
            {
                $content = $updatedContent
                if($changedFile -eq $False) {
                    Write-Information "* $fileName"
                    $changedFile = $True
                }

                Write-Information "   - Changed $replacement comment to SuppressMessage"
            }
        }


        $replacementText = '${Start}' + $emptyLine + '${End}'
        $updatedContent = $content -replace $removeBlankLinesRegex, $replacementText
        if($content -ne $updatedContent)
        {
            $content = $updatedContent
            if($changedFile -eq $False) {
                Write-Information "* $fileName"
                $changedFile = $True
            }

            Write-Information "   - Removed blank lines (end tag)"
        }


        $replacementText = '${Start}' + $emptyLine + '${End}'
        $updatedContent = $content -replace $removeBlankLines2Regex, $replacementText
        if($content -ne $updatedContent)
        {
            $content = $updatedContent
            if($changedFile -eq $False) {
                Write-Information "* $fileName"
                $changedFile = $True
            }

            Write-Information "   - Removed blank lines (single tag)"
        }

        if($content -ne $originalContent) {
            Set-Content -Path $fileName -Value $content
        }
    }
}
#endif
}