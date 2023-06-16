using System.Net.Http.Headers;

internal static class FunctionCallHttpExtensions
{
    public static HttpRequestMessage ToHttpRequestMessage( this FunctionCall functionCall, string functionName, string gatewayUrl )
    {
        var httpContent = new ByteArrayContent(
            functionCall.Content?.ToArray() ?? Array.Empty<byte>()
        );

        var authorization = functionCall.Metadata.GetValueOrDefault( "Authorization", string.Empty );

        if ( !string.IsNullOrEmpty( authorization ) )
        {
            httpContent.Headers.Add( "Authorization", authorization );
        }

        foreach ( var header in functionCall.Metadata.Where( x => x.Key.StartsWith( "HTTP_HEADER_" )) )
        {
            try
            {
                httpContent.Headers.Add( header.Key.Substring( "HTTP_HEADER_".Length ), header.Value );
            }
            catch {}
        }

        httpContent.Headers.ContentType = functionCall.ContentType != null
            ? new MediaTypeHeaderValue( functionCall.ContentType )
            : null;

        var method = functionCall.Metadata.GetValueOrDefault( "HTTP_METHOD", "POST" );
        var path = functionCall.Metadata.GetValueOrDefault( "HTTP_PATH", "/" );

        if ( functionCall.Arguments.Any() )
        {
            var args = string.Join( 
                '&',
                functionCall.Arguments.Select( arg => string.IsNullOrEmpty( arg.Value )
                    ? arg.Key
                    : $"{arg.Key}={System.Web.HttpUtility.UrlEncode( arg.Value )}" )
            );

            path = string.Concat( path, "?", args );
        }

        var uri = $"{gatewayUrl.TrimEnd( '/' )}/proxy/{functionName}{path}";

        return new HttpRequestMessage( new HttpMethod( method ), uri );
    }
}
