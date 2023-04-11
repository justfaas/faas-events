using System.Net.Http.Headers;
using System.Text.Json;

internal static class EventUnwrapExtensions
{
    public static ( string, HttpRequestMessage ) UnwrapFunctionCall( this Event faasEvent, string gatewayUrl )
    {
        var functionCall = JsonSerializer.Deserialize<FunctionCall>( faasEvent.Content );

        if ( functionCall == null )
        {
            // unable to deserialize...
            throw new JsonException( "Failed to deserialize event as a 'FunctionCall'." );
        }

        var function = string.Join( '/', functionCall.Namespace ?? "default", functionCall.Name );

        return ( function, functionCall.ToHttpRequestMessage( function, gatewayUrl ) );
    }

    public static HttpRequestMessage UnwrapEvent( this Event faasEvent, string gatewayUrl, string function )
    {
        var httpContent = new ByteArrayContent(
            faasEvent.Content ?? Array.Empty<byte>()
        );

        if ( faasEvent.ContentType != null )
        {
            try
            {
                httpContent.Headers.ContentType = MediaTypeHeaderValue.Parse( faasEvent.ContentType );
            }
            catch { }
        }

        return new HttpRequestMessage( HttpMethod.Post, $"{gatewayUrl}/proxy/{function}" );
    }
}
