using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Helpers;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed partial class SourceFileSuppressionRemover : ISourceFileSuppressionRemover
{
    private readonly IDotNetBuild _dotNetBuild;
    private readonly ILogger<SourceFileSuppressionRemover> _logger;

    public SourceFileSuppressionRemover(IDotNetBuild dotNetBuild, ILogger<SourceFileSuppressionRemover> logger)
    {
        this._dotNetBuild = dotNetBuild;
        this._logger = logger;
    }

    public async ValueTask<string> RemoveSuppressionsAsync(
        string fileName,
        string content,
        BuildContext buildContext,
        CancellationToken cancellationToken
    )
    {
        MatchCollection matches = SuppressMessages().Matches(content);

        if (matches.Count == 0)
        {
            return content;
        }

        // Captured up front so a revert to the unmodified content can restore the file
        // byte-exactly, regardless of its original encoding/BOM.
        byte[] originalBytes = await File.ReadAllBytesAsync(path: fileName, cancellationToken: cancellationToken);

        string successfulBuild = content;
        bool hasSuccessfulEdit = false;

        // go from the last match backwards to ensure that the positions always match
        foreach (Match match in matches.Reverse())
        {
            string testSource = RemoveSuppressionFromMatch(match: match, source: successfulBuild);

            await File.WriteAllTextAsync(
                path: fileName,
                contents: testSource,
                encoding: TextEncoding.Utf8NoBom,
                cancellationToken: cancellationToken
            );

            try
            {
                await this._dotNetBuild.BuildAsync(buildContext: buildContext, cancellationToken: cancellationToken);
                successfulBuild = testSource;
                hasSuccessfulEdit = true;
            }
            catch (Exception exception)
            {
                // Revert to the last successful build on disk
                if (hasSuccessfulEdit)
                {
                    await File.WriteAllTextAsync(
                        path: fileName,
                        contents: successfulBuild,
                        encoding: TextEncoding.Utf8NoBom,
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    await File.WriteAllBytesAsync(
                        path: fileName,
                        bytes: originalBytes,
                        cancellationToken: cancellationToken
                    );
                }

                // build failed without this suppression, so skip it the revert
                this._logger.FailedToBuild(filename: fileName, message: exception.Message, exception: exception);
            }
        }

        return successfulBuild;
    }

    private static string RemoveSuppressionFromMatch(Match match, string source)
    {
        string before = source[..match.Index];
        int endPos = match.Index + match.Length;
        string after = source[endPos..];

        string testSource = before + after;

        return testSource;
    }

    [GeneratedRegex(
        pattern: "\\[\\s*(assembly:)?\\s*SuppressMessage\\(.*?\\)\\s*\\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 5000
    )]
    private static partial Regex SuppressMessages();
}
