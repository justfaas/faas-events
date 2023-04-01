using System.Text.Json.Serialization;

public sealed class FunctionCall
{
    [JsonConverter( typeof( DictionaryJsonConverter<string, string> ) )]
    public Dictionary<string, string> Arguments { get; set; } = new Dictionary<string, string>();

    [JsonConverter( typeof( EnumerableJsonConverter<byte> ) )]
    public IEnumerable<byte>? Content { get; set; }

    public string? ContentType { get; set; }

    [JsonConverter( typeof( DictionaryJsonConverter<string, string> ) )]
    public Dictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
