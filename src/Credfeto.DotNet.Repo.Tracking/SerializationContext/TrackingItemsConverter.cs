using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tracking.SerializationContext;

internal sealed class TrackingItemsConverter : JsonConverter<TrackingItems>
{
    [SuppressMessage(
        category: "Style",
        checkId: "IDE0028: Collection initialization can be simplified",
        Justification = "A collection expression cannot pass an IEqualityComparer to the Dictionary constructor; simplifying would silently drop OrdinalIgnoreCase and change lookup semantics"
    )]
    public override TrackingItems Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return ThrowInvalidJsonToken();
        }

        // step forward
        reader.Read();

        Dictionary<string, string> instance = new(StringComparer.OrdinalIgnoreCase);

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            (string key, string value) = ReadEntry(reader: ref reader);

            if (!instance.TryAdd(key: key, value: value))
            {
                return ThrowDuplicateKey(key);
            }
        }

        return new(instance);
    }

    [DoesNotReturn]
    private static TrackingItems ThrowInvalidJsonToken()
    {
        throw new JsonException(message: "Invalid Json token");
    }

    public override void Write(Utf8JsonWriter writer, TrackingItems value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();

        foreach (
            (string packageId, string version) in value.Cache.OrderBy(
                keySelector: x => x.Key,
                comparer: StringComparer.OrdinalIgnoreCase
            )
        )
        {
            writer.WriteString(propertyName: packageId, value: version);
        }

        writer.WriteEndObject();
    }

    [DoesNotReturn]
    private static TrackingItems ThrowDuplicateKey(string key)
    {
        throw new JsonException(message: $"Duplicate key: {key}");
    }

    private static (string Key, string Value) ReadEntry(ref Utf8JsonReader reader)
    {
        string key = ReadString(reader: ref reader, expectedTokenType: JsonTokenType.PropertyName);

        reader.Read();

        string value = ReadString(reader: ref reader, expectedTokenType: JsonTokenType.String);

        reader.Read();

        return (key, value);
    }

    private static string ReadString(ref Utf8JsonReader reader, JsonTokenType expectedTokenType)
    {
        if (reader.TokenType != expectedTokenType)
        {
            return RequiredString();
        }

        // ! GetString only returns null for a Null token, which the token type check above has already rejected
        return reader.GetString()!;
    }

    [DoesNotReturn]
    private static string RequiredString()
    {
        throw new JsonException(message: "Invalid Json token");
    }
}
