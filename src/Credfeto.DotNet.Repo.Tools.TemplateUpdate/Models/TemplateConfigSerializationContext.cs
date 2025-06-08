using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

[SuppressMessage(category: "ReSharper", checkId: "PartialTypeWithSinglePart", Justification = "Required for JsonSerializerContext")]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization | JsonSourceGenerationMode.Metadata,
                             PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
                             DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                             WriteIndented = false,
                             IncludeFields = false)]
[JsonSerializable(typeof(TemplateConfig))]
internal sealed partial class TemplateConfigSerializationContext : JsonSerializerContext;