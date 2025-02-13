using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.Release.Models;

[SuppressMessage(
    category: "ReSharper",
    checkId: "PartialTypeWithSinglePart",
    Justification = "Required for JsonSerializerContext"
)]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Serialization | JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    IncludeFields = false
)]
[JsonSerializable(typeof(ReleaseConfiguration))]
[JsonSerializable(typeof(ReleaseConfigSettings))]
[JsonSerializable(typeof(RepoConfigMatch))]
[JsonSerializable(typeof(IReadOnlyList<RepoConfigMatch>))]
internal sealed partial class ReleaseConfigSerializationContext : JsonSerializerContext;
