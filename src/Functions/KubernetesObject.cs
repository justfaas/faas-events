using System.Text.Json;

internal sealed class KubernetesObject
{
    public MetadataObject? Metadata { get; set; }
}

internal sealed class MetadataObject
{
    public Dictionary<string, string>? Annotations { get; set; }
}

internal static class KubernetesObjectCloudEventExtensions
{
    private static JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static KubernetesObject? ReadDataAsKubernetesObject( this CloudNative.CloudEvents.CloudEvent cloudEvent )
    {
        var type = cloudEvent.Data!.GetType();

        if ( type == typeof( string ) )
        {
            return JsonSerializer.Deserialize<KubernetesObject>(
                (string)cloudEvent.Data,
                jsonSerializerOptions
            );
        }

        if ( typeof( IEnumerable<byte> ).IsAssignableFrom( type ) )
        {
            return JsonSerializer.Deserialize<KubernetesObject>( 
                ( (IEnumerable<byte>)cloudEvent.Data ).ToArray(),
                jsonSerializerOptions
            );
        }

        if ( type == typeof( JsonElement ) )
        {
            return ( (JsonElement)cloudEvent.Data ).Deserialize<KubernetesObject>( jsonSerializerOptions );
        }

        throw new NotSupportedException( $"Don't know how to handle with '{type.Name}' type." );
    }
}
