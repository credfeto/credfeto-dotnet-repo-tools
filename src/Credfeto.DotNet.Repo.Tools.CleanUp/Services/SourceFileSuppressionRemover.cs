using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
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

    public async ValueTask<string> RemoveSuppressionsAsync(string fileName, string content, BuildContext buildContext, CancellationToken cancellationToken)
    {
        MatchCollection matches = SuppressMessages()
            .Matches(content);

        string successfulBuild = content;

        foreach (Match match in matches.Reverse())
        {
            string before = successfulBuild[..match.Index];
            int endPos = match.Index + match.Length;
            string after = successfulBuild[endPos..];

            string testSource = before + after;

            await File.WriteAllTextAsync(path: fileName, contents: testSource, encoding: Encoding.UTF8, cancellationToken: cancellationToken);

            try
            {
                await this._dotNetBuild.BuildAsync(basePath: buildContext.SourceDirectory,
                                                   buildSettings: buildContext.BuildSettings,
                                                   buildOverride: buildContext.BuildOverride,
                                                   cancellationToken: cancellationToken);
                successfulBuild = before + after;
            }
            catch (Exception exception)
            {
                // build failed without this suppression so skip it.
                this._logger.LogDebug(exception: exception, message: "Failed to build");
                Debug.WriteLine(exception.Message);
            }
        }

        await Task.Delay(millisecondsDelay: 1, cancellationToken: cancellationToken);

        return content;
    }

    [GeneratedRegex(pattern: "\\[SuppressMessage\\(.*\\)\\]", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline, matchTimeoutMilliseconds: 5000)]
    private static partial Regex SuppressMessages();
}