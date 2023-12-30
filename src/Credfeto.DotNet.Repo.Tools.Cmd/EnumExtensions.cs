using System.Diagnostics.CodeAnalysis;
using Credfeto.Enumeration.Source.Generation.Attributes;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Cmd;

[EnumText(typeof(LogLevel))]
[SuppressMessage(category: "ReSharper", checkId: "PartialTypeWithSinglePart", Justification = "Needed for generated code")]
internal static partial class EnumExtensions
{
    // Code generated
}