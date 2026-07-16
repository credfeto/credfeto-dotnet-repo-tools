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

    public static string ToProjectFileText(XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        StringBuilder builder = new();

        using (XmlWriter xmlWriter = XmlWriter.Create(output: builder, settings: CreateSettings(isAsync: false)))
        {
            document.Save(xmlWriter);

            // XmlDocument never writes anything after the root element's closing tag, so this is
            // always the file's last character - no need to check for or trim any existing trailing newline.
            xmlWriter.WriteWhitespace("\n");
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
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(encoding);

        XmlWriterSettings settings = CreateSettings(isAsync: true);
        settings.Encoding = encoding;

        await using (MemoryStream stream = new())
        {
            await using (XmlWriter xmlWriter = XmlWriter.Create(output: stream, settings: settings))
            {
                document.Save(xmlWriter);

                // XmlDocument never writes anything after the root element's closing tag, so this is
                // always the file's last byte - no need to check for or trim any existing trailing newline.
                await xmlWriter.WriteWhitespaceAsync("\n");
            }

            // Render fully in memory first, so a serialization failure can never truncate an
            // already-good file on disk.
            await File.WriteAllBytesAsync(
                path: filePath,
                bytes: stream.ToArray(),
                cancellationToken: cancellationToken
            );
        }
    }

    private static XmlWriterSettings CreateSettings(bool isAsync)
    {
        return new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineOnAttributes = false,
            OmitXmlDeclaration = true,
            Async = isAsync,
        };
    }
}
