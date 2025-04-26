using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services.LoggingExtensions;
using CSharpier.Core;
using CSharpier.Core.CSharp;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed class SourceFileReformatter : ISourceFileReformatter
{
    private static readonly CodeFormatterOptions Options = new()
                                                           {
                                                               Width = 120,
                                                               IndentStyle = IndentStyle.Spaces,
                                                               EndOfLine = EndOfLine.Auto,
                                                               IncludeGenerated = false,
                                                               IndentSize = 4
                                                           };

    private readonly ILogger<SourceFileReformatter> _logger;

    public SourceFileReformatter(ILogger<SourceFileReformatter> logger)
    {
        this._logger = logger;
    }

    public async ValueTask<string> ReformatAsync(string content, string fileName, CancellationToken cancellationToken)
    {
        CodeFormatterResult formatted = await CSharpFormatter.FormatAsync(code: content, options: Options, cancellationToken: cancellationToken);

        if (formatted.CompilationErrors.Any())
        {
            this._logger.FormattingErrorsFound(fileName);

            return content;
        }

        return formatted.Code;
    }
}