using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Credfeto.DotNet.Repo.Tools.Extensions;

public static class ProjectXmlSerializer
{
    private static readonly XmlWriterSettings WriterSettings = new()
                                                               {
                                                                   Async = true,
                                                                   Indent = true,
                                                                   IndentChars = "  ",
                                                                   OmitXmlDeclaration = true,
                                                                   Encoding = Encoding.UTF8,
                                                                   NewLineHandling = NewLineHandling.None,
                                                                   NewLineOnAttributes = false,
                                                                   NamespaceHandling = NamespaceHandling.OmitDuplicates,
                                                                   CloseOutput = true,
                                                               };

    public static async ValueTask<string> ToProjectFileTextAsync(
        XmlDocument document,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        StringBuilder builder = new();

        await using (XmlWriter xmlWriter = XmlWriter.Create(output: builder, settings: WriterSettings))
        {
            await WriteCommonAsync(document: document, xmlWriter: xmlWriter);
        }

        return builder.ToString();
    }

    private static Task WriteCommonAsync(XmlDocument document, XmlWriter xmlWriter)
    {
        document.Save(xmlWriter);

        // XmlDocument never writes anything after the root element's closing tag, so this is
        // always the file's last character - no need to check for or trim any existing trailing newline.
        return xmlWriter.WriteWhitespaceAsync("\n");
    }

    public static async ValueTask WriteAsync(string filePath, string content, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
            path: filePath,
            contents: content,
            encoding: WriterSettings.Encoding,
            cancellationToken: cancellationToken
        );
    }

    public static async ValueTask SaveAsync(
        XmlDocument document,
        string filePath,
        CancellationToken cancellationToken
    )
    {
        await using (MemoryStream stream = new())
        {
            await using (XmlWriter writer = XmlWriter.Create(output: stream, settings: WriterSettings))
            {
                await WriteCommonAsync(document: document, xmlWriter: writer);
            }

            // Render fully in memory first, so a serialisation failure can never truncate an
            // already-good file on disk.
            await File.WriteAllBytesAsync(path: filePath, bytes: stream.ToArray(), cancellationToken: cancellationToken);
        }
    }
}
