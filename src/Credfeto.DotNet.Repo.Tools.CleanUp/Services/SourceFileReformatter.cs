using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services.LoggingExtensions;
using CSharpier.Core;
using CSharpier.Core.CSharp;
using Microsoft.CodeAnalysis;
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
        try
        {
            CodeFormatterResult formatted = await CSharpFormatter.FormatAsync(code: content, options: Options, cancellationToken: cancellationToken);

            IReadOnlyList<Diagnostic> errors = [..formatted.CompilationErrors];

            if (errors is [])
            {
                return formatted.Code;
            }

            this._logger.FormattingErrorsFound(filename: fileName, errors: errors);

            return content;
        }
        catch (Exception exception)
        {
            this._logger.FormattingErrorsFound(filename: fileName, message: exception.Message, exception: exception);

            return content;
        }
    }
}