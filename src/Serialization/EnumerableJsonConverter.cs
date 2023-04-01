using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class EnumerableJsonConverter<T> : JsonConverter<IEnumerable<T>>
{
    public override bool HandleNull => true;

    public override IEnumerable<T>? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
    {
        if ( reader.TokenType == JsonTokenType.Null )
        {
            return Enumerable.Empty<T>();
        }

        var value = ( (JsonConverter<IEnumerable<T>>)options.GetConverter( typeof( IEnumerable<T> ) ) )
            .Read( ref reader, typeof( IEnumerable<T> ), options )!;

        return ( value );
    }

    public override void Write( Utf8JsonWriter writer, IEnumerable<T> value, JsonSerializerOptions options )
    {
        throw new NotImplementedException();
    }
}
