using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Models;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Serialization | JsonSourceGenerationMode.Metadata,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    IncludeFields = false
)]
[JsonSerializable(typeof(GlobalJsonPacket))]
[SuppressMessage(
    category: "ReSharper",
    checkId: "PartialTypeWithSinglePart",
    Justification = "Required for " + nameof(JsonSerializerContext) + " code generation"
)]
internal sealed partial class GlobalJsonJsonSerializerContext : JsonSerializerContext;
