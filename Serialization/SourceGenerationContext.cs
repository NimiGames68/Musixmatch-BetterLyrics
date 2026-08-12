using System.Text.Json;
using System.Text.Json.Serialization;

namespace BetterLyrics.Plugins.Source.Musixmatch.Serialization
{
    [JsonSerializable(typeof(JsonElement))]
    [JsonSourceGenerationOptions(WriteIndented = false)]
    public partial class SourceGenerationContext : JsonSerializerContext { }
}
