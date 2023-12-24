using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tracking.SerializationContext;

[JsonConverter(typeof(TrackingItemsConverter))]
internal sealed class TrackingItems
{
    private readonly Dictionary<string, string> _cache;

    public TrackingItems(Dictionary<string, string> cache)
    {
        this._cache = cache;
    }

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> Cache => this._cache;
}