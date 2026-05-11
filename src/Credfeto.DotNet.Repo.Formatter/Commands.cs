using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Cocona;
using Credfeto.DotNet.Repo.Formatter.LoggingExtensions;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Formatter;

[SuppressMessage(
    category: "Microsoft.Performance",
    checkId: "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Instantiated by Cocona"
)]
internal sealed class Commands
{
    private static readonly IReadOnlyList<string> SupportedExtensions = [".cs", ".csproj"];

    private readonly IDotNetBuild _dotNetBuild;
    private readonly IDotNetFilesDetector _dotNetFilesDetector;
    private readonly ILogger<Commands> _logger;
    private readonly IProjectXmlRewriter _projectXmlRewriter;
    private readonly IResharperSuppressionToSuppressMessage _resharperSuppressionToSuppressMessage;
    private readonly ISourceFileReformatter _sourceFileReformatter;
    private readonly ISourceFileSuppressionRemover _sourceFileSuppressionRemover;
    private readonly IXmlDocCommentRemover _xmlDocCommentRemover;
    private readonly CancellationToken _cancellationToken = CancellationToken.None;

    [SuppressMessage(
        category: "FunFair.CodeAnalysis",
        checkId: "FFS0023: Use ILogger rather than ILogger<T>",
        Justification = "Needed in this case"
    )]
    public Commands(
        IProjectXmlRewriter projectXmlRewriter,
        ISourceFileReformatter sourceFileReformatter,
        IXmlDocCommentRemover xmlDocCommentRemover,
        IResharperSuppressionToSuppressMessage resharperSuppressionToSuppressMessage,
        ISourceFileSuppressionRemover sourceFileSuppressionRemover,
        IDotNetBuild dotNetBuild,
        IDotNetFilesDetector dotNetFilesDetector,
        ILogger<Commands> logger
    )
    {
        this._projectXmlRewriter = projectXmlRewriter;
        this._sourceFileReformatter = sourceFileReformatter;
        this._xmlDocCommentRemover = xmlDocCommentRemover;
        this._resharperSuppressionToSuppressMessage = resharperSuppressionToSuppressMessage;
        this._sourceFileSuppressionRemover = sourceFileSuppressionRemover;
        this._dotNetBuild = dotNetBuild;
        this._dotNetFilesDetector = dotNetFilesDetector;
        this._logger = logger;
    }

    [Command(Description = "Format C# source files and project files")]
    public async Task<int> CleanupAsync(
        [Argument(Description = "Files, globs, or folders to process")] string[] inputs,
        [Option(
            name: "remove-suppressions",
            Description = "Remove redundant [SuppressMessage] attributes (requires --build-root)"
        )]
            bool removeSuppressions = false,
        [Option(
            name: "build-root",
            Description = "Root directory used for dotnet build when --remove-suppressions is enabled"
        )]
            string? buildRoot = null
    )
    {
        if (removeSuppressions && string.IsNullOrWhiteSpace(buildRoot))
        {
            this._logger.LogBuildRootRequired();

            return Constants.ExitCodes.Error;
        }

        if (!string.IsNullOrWhiteSpace(buildRoot) && !Directory.Exists(buildRoot))
        {
            this._logger.LogBuildRootNotFound(buildRoot);

            return Constants.ExitCodes.Error;
        }

        IReadOnlyList<string> resolvedFiles = ResolveInputs(inputs);

        if (resolvedFiles is [])
        {
            this._logger.LogNoInputFiles();

            return Constants.ExitCodes.Error;
        }

        if (!this.ValidateFileTypes(resolvedFiles))
        {
            return Constants.ExitCodes.Error;
        }

        BuildContext? buildContext = await this.BuildContextOrNullAsync(
            removeSuppressions: removeSuppressions,
            buildRoot: buildRoot
        );

        int updatedCount = 0;

        foreach (string file in resolvedFiles)
        {
            this._logger.LogProcessingFile(file);

            bool updated = await this.ProcessFileAsync(filePath: file, buildContext: buildContext);

            if (updated)
            {
                ++updatedCount;
                this._logger.LogFileUpdated(file);
            }
            else
            {
                this._logger.LogFileUnchanged(file);
            }
        }

        this._logger.LogCompleted(fileCount: resolvedFiles.Count, updatedCount: updatedCount);

        return Constants.ExitCodes.Success;
    }

    private async ValueTask<BuildContext?> BuildContextOrNullAsync(
        bool removeSuppressions,
        string? buildRoot
    )
    {
        if (!removeSuppressions || buildRoot is null)
        {
            return null;
        }

        DotNetFiles dotNetFiles = await this._dotNetFilesDetector.FindAsync(
            baseFolder: buildRoot,
            cancellationToken: this._cancellationToken
        );

        BuildSettings buildSettings = await this._dotNetBuild.LoadBuildSettingsAsync(
            projects: dotNetFiles.Projects,
            cancellationToken: this._cancellationToken
        );

        return new BuildContext(
            SourceDirectory: buildRoot,
            BuildSettings: buildSettings,
            BuildOverride: new BuildOverride(PreRelease: true)
        );
    }

    private async ValueTask<bool> ProcessFileAsync(string filePath, BuildContext? buildContext)
    {
        string extension = Path.GetExtension(filePath);

        if (StringComparer.OrdinalIgnoreCase.Equals(x: extension, y: ".cs"))
        {
            return await this.ProcessCSharpFileAsync(
                filePath: filePath,
                buildContext: buildContext
            );
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(x: extension, y: ".csproj"))
        {
            return await this.ProcessProjectFileAsync(filePath);
        }

        return false;
    }

    private async ValueTask<bool> ProcessCSharpFileAsync(
        string filePath,
        BuildContext? buildContext
    )
    {
        string original = await File.ReadAllTextAsync(
            path: filePath,
            encoding: Encoding.UTF8,
            cancellationToken: this._cancellationToken
        );

        string content = original;
        content = this._resharperSuppressionToSuppressMessage.Replace(content);
        content = this._xmlDocCommentRemover.RemoveXmlDocComments(content);
        content = await this._sourceFileReformatter.ReformatAsync(
            fileName: filePath,
            content: content,
            cancellationToken: this._cancellationToken
        );

        if (buildContext is { } ctx)
        {
            content = await this._sourceFileSuppressionRemover.RemoveSuppressionsAsync(
                fileName: filePath,
                content: content,
                buildContext: ctx,
                cancellationToken: this._cancellationToken
            );
        }

        if (StringComparer.Ordinal.Equals(x: original, y: content))
        {
            return false;
        }

        await File.WriteAllTextAsync(
            path: filePath,
            contents: content,
            encoding: Encoding.UTF8,
            cancellationToken: this._cancellationToken
        );

        return true;
    }

    private async ValueTask<bool> ProcessProjectFileAsync(string filePath)
    {
        string original = await File.ReadAllTextAsync(
            path: filePath,
            encoding: Encoding.UTF8,
            cancellationToken: this._cancellationToken
        );

        XmlDocument doc = new();
        doc.LoadXml(original);

        bool changed = this._projectXmlRewriter.ReOrderPropertyGroups(
            projectDocument: doc,
            filename: filePath
        );

        changed |= this._projectXmlRewriter.ReOrderIncludes(
            projectDocument: doc,
            filename: filePath
        );

        if (!changed)
        {
            return false;
        }

        XmlWriterSettings settings = new()
        {
            Indent = true,
            IndentChars = "  ",
            NewLineOnAttributes = false,
            OmitXmlDeclaration = true,
            Async = true,
        };

        await using (
            XmlWriter xmlWriter = XmlWriter.Create(outputFileName: filePath, settings: settings)
        )
        {
            doc.Save(xmlWriter);
        }

        string rewritten = await File.ReadAllTextAsync(
            path: filePath,
            encoding: Encoding.UTF8,
            cancellationToken: this._cancellationToken
        );

        return !StringComparer.Ordinal.Equals(x: original, y: rewritten);
    }

    private bool ValidateFileTypes(IReadOnlyList<string> files)
    {
        bool valid = true;

        foreach (string file in files)
        {
            string extension = Path.GetExtension(file);

            if (
                !SupportedExtensions.Contains(
                    value: extension,
                    comparer: StringComparer.OrdinalIgnoreCase
                )
            )
            {
                this._logger.LogInvalidFileType(file);
                valid = false;
            }
        }

        return valid;
    }

    private static IReadOnlyList<string> ResolveInputs(IEnumerable<string> inputs)
    {
        List<string> resolved = [];

        foreach (string input in inputs)
        {
            if (Directory.Exists(input))
            {
                resolved.AddRange(ScanDirectory(input));
            }
            else if (ContainsWildcard(input))
            {
                resolved.AddRange(ExpandGlob(input));
            }
            else
            {
                resolved.Add(input);
            }
        }

        return
        [
            .. resolved
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static IEnumerable<string> ScanDirectory(string directory)
    {
        return Directory
            .EnumerateFiles(
                path: directory,
                searchPattern: "*.*",
                searchOption: SearchOption.AllDirectories
            )
            .Where(f =>
            {
                string ext = Path.GetExtension(f);

                return (
                        StringComparer.OrdinalIgnoreCase.Equals(x: ext, y: ".cs")
                        || StringComparer.OrdinalIgnoreCase.Equals(x: ext, y: ".csproj")
                    ) && !IsGeneratedFile(f);
            });
    }

    private static IEnumerable<string> ExpandGlob(string glob)
    {
        string baseDirectory = GetGlobBaseDirectory(glob);
        string pattern =
            glob.Length > baseDirectory.Length
                ? glob[baseDirectory.Length..]
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : glob;

        Matcher matcher = new();
        matcher.AddInclude(pattern);

        return matcher.GetResultsInFullPath(baseDirectory);
    }

    private static string GetGlobBaseDirectory(string glob)
    {
        int firstWildcard = glob.IndexOfAny(['*', '?']);

        if (firstWildcard < 0)
        {
            return Directory.GetCurrentDirectory();
        }

        string beforeWildcard = glob[..firstWildcard];
        int lastSeparator = beforeWildcard.LastIndexOfAny([
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
        ]);

        return lastSeparator < 0 ? Directory.GetCurrentDirectory() : glob[..lastSeparator];
    }

    private static bool ContainsWildcard(string input)
    {
        return input.Contains(value: '*', comparisonType: StringComparison.Ordinal)
            || input.Contains(value: '?', comparisonType: StringComparison.Ordinal);
    }

    private static bool IsGeneratedFile(string filePath)
    {
        return filePath.Contains(
                value: Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                comparisonType: StringComparison.Ordinal
            )
            || filePath.Contains(
                value: Path.DirectorySeparatorChar + "generated" + Path.DirectorySeparatorChar,
                comparisonType: StringComparison.Ordinal
            )
            || filePath.Contains(value: ".generated.", comparisonType: StringComparison.Ordinal);
    }
}
