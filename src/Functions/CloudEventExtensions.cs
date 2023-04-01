using System.Text.Json;

internal static class FunctionCallCloudEventExtensions
{
    private static JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static bool IsFunctionCall( this CloudNative.CloudEvents.CloudEvent cloudEvent )
    {
        var isFunctionCall = cloudEvent.Type!.Equals( KnownCloudEventTypes.FunctionInvoked ) == true;
        var hasSubject = cloudEvent.Subject != null;
        var hasData = cloudEvent.Data != null;
        var hasValidDataContentType = cloudEvent.DataContentType == null || cloudEvent.DataContentType.Equals( "application/json" );

        return ( isFunctionCall && hasSubject && hasData && hasValidDataContentType );
    }

    public static FunctionCall? ToFunctionCall( this CloudNative.CloudEvents.CloudEvent cloudEvent )
    {
        if ( !IsFunctionCall( cloudEvent ) )
        {
            throw new ArgumentException( "Event is not a function call." );
        }

        var type = cloudEvent.Data!.GetType();

        if ( type == typeof( string ) )
        {
            return JsonSerializer.Deserialize<FunctionCall>(
                (string)cloudEvent.Data,
                jsonSerializerOptions
            );
        }

        if ( typeof( IEnumerable<byte> ).IsAssignableFrom( type ) )
        {
            return JsonSerializer.Deserialize<FunctionCall>( 
                ( (IEnumerable<byte>)cloudEvent.Data ).ToArray(),
                jsonSerializerOptions
            );
        }

        if ( type == typeof( JsonElement ) )
        {
            return ( (JsonElement)cloudEvent.Data ).Deserialize<FunctionCall>( jsonSerializerOptions );
        }

        throw new NotSupportedException( $"Don't know how to handle with '{type.Name}' type." );
    }
}
