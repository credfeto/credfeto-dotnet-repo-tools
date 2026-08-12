using System.Text;

namespace Credfeto.DotNet.Repo.Tools.Extensions;

public static class TextEncoding
{
    public static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
}
