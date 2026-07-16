using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Credfeto.DotNet.Repo.Tools.Extensions;

public static class ProjectXmlSerializer
{
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false
    );

    private static readonly XmlWriterSettings Settings = new()
    {
        Indent = true,
        IndentChars = "  ",
        NewLineOnAttributes = false,
        OmitXmlDeclaration = true,
        Async = false,
    };

    public static string ToProjectFileText(XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        StringBuilder builder = new();

        using (XmlWriter xmlWriter = XmlWriter.Create(output: builder, settings: Settings))
        {
            document.Save(xmlWriter);
        }

        return EnsureSingleTrailingNewLine(builder.ToString());
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

    public static ValueTask SaveAsync(
        XmlDocument document,
        string filePath,
        Encoding encoding,
        in CancellationToken cancellationToken
    )
    {
        string content = ToProjectFileText(document);

        return WriteAsync(
            filePath: filePath,
            content: content,
            encoding: encoding,
            cancellationToken: cancellationToken
        );
    }

    private static string EnsureSingleTrailingNewLine(string content)
    {
        return content.TrimEnd('\r', '\n') + "\n";
    }
}
