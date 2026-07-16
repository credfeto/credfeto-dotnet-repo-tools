using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Credfeto.DotNet.Repo.Tools.Extensions;

public static class ProjectXmlSerializer
{
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static async ValueTask<string> ToProjectFileTextAsync(
        XmlDocument document,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        StringBuilder builder = new();

        await using (XmlWriter xmlWriter = XmlWriter.Create(output: builder, settings: CreateSettings()))
        {
            document.Save(xmlWriter);

            // XmlDocument never writes anything after the root element's closing tag, so this is
            // always the file's last character - no need to check for or trim any existing trailing newline.
            await xmlWriter.WriteWhitespaceAsync("\n");
        }

        return builder.ToString();
    }

    public static async ValueTask WriteAsync(
        string filePath,
        string content,
        Encoding encoding,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(encoding);

        await File.WriteAllTextAsync(
            path: filePath,
            contents: content,
            encoding: encoding,
            cancellationToken: cancellationToken
        );
    }

    public static async ValueTask SaveAsync(
        XmlDocument document,
        string filePath,
        Encoding encoding,
        CancellationToken cancellationToken
    )
    {
        // Fully rendered in memory first, so a serialization failure can never truncate an
        // already-good file on disk.
        string content = await ToProjectFileTextAsync(document: document, cancellationToken: cancellationToken);

        await WriteAsync(
            filePath: filePath,
            content: content,
            encoding: encoding,
            cancellationToken: cancellationToken
        );
    }

    private static XmlWriterSettings CreateSettings()
    {
        return new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineOnAttributes = false,
            OmitXmlDeclaration = true,
            Async = true,
        };
    }
}
