using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class DictionaryJsonConverter<TKey, TValue> : JsonConverter<Dictionary<TKey, TValue>> where TKey : notnull
{
    private JsonConverter<TKey>? keyConverter;
    private JsonConverter<TValue>? valueConverter;
    private readonly Type keyType = typeof( TKey );
    private readonly Type valueType = typeof( TValue );

    public override bool HandleNull => true;

    public override Dictionary<TKey, TValue>? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
    {
        if ( reader.TokenType == JsonTokenType.Null )
        {
            return new Dictionary<TKey, TValue>();
        }

        if ( reader.TokenType != JsonTokenType.StartObject )
        {
            throw new JsonException();
        }

        var dictionary = new Dictionary<TKey, TValue>();
        keyConverter = keyConverter ?? (JsonConverter<TKey>)options.GetConverter( typeof( TKey ) );
        valueConverter = valueConverter ?? (JsonConverter<TValue>)options.GetConverter( typeof( TValue ) );

        while ( reader.Read() )
        {
            if ( reader.TokenType == JsonTokenType.EndObject )
            {
                return ( dictionary );
            }

            // read key
            if ( reader.TokenType != JsonTokenType.PropertyName )
            {
                throw new JsonException();
            }

            TKey key = keyConverter.Read( ref reader, keyType, options )!;

            // read value
            reader.Read();
            TValue value = valueConverter.Read(ref reader, valueType, options)!;

            // Add to dictionary
            dictionary.Add( key, value );
        }

        throw new JsonException();
    }

    public override void Write( Utf8JsonWriter writer, Dictionary<TKey, TValue> value, JsonSerializerOptions options )
    {
        throw new NotImplementedException();
    }
}
