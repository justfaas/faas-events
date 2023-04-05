using System.Text;
using System.Text.Json;

internal static class EventsApiEndpoints
{
    private static ILogger logger = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
        .CreateLogger( "null" );

    public static IEndpointRouteBuilder MapEventsApi( this IEndpointRouteBuilder builder )
    {
        builder.MapPost( "/apis/events", PublishAsync );

        logger = builder.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger( "Apis.Events" );

        return ( builder );
    }

    private static async Task<IResult> PublishAsync( HttpRequest httpRequest, NATSService nats )
    {
        // TODO: should we support sending the event in the body as json?
        // if so, we need a "deserializer". if the headers are in place, we use the haders
        // but if the headers aren't in place and content type is json, we attempt to deserialize
        // the content as an Event. Could be useful...??

        var eventType = httpRequest.Headers.GetValueOrDefault( "X-Event-Type" );

        if ( string.IsNullOrEmpty( eventType ) )
        {
            // TODO: give error details
            return Results.BadRequest();
        }

        // create event and serialize as json
        var faasEvent = new Event
        {
            EventType = eventType,
            EventSource = httpRequest.Headers.GetValueOrDefault( "X-Event-Source" ),
            Content = httpRequest.BodyReader.ReadAsByteArray(),
            ContentType = httpRequest.ContentType,
            WebhookUrl = httpRequest.Headers.GetValueOrDefault( "X-Event-Webhook-Url" )
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes( faasEvent );

        // publish event
        try
        {
            var jetStream = nats.GetConnection()
                .CreateJetStreamContext();

            var publishType = faasEvent.EventType;

            if ( !( publishType?.StartsWith( "com.justfaas.function." ) == true ) )
            {
                publishType = KnownCloudEventTypes.EventAdded;
            }

            await jetStream.PublishAsync( publishType, bytes );
        }
        catch ( Exception ex )
        {
            logger.LogError( ex, ex.Message );

            // TODO: return details on response body
            return Results.StatusCode( 500 );
        }

        return Results.Accepted();
    }
}
