using System.Text;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.AspNetCore;
using CloudNative.CloudEvents.SystemTextJson;

internal static class EventsApiEndpoints
{
    private static ILogger logger = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
        .CreateLogger( "null" );

    private static CloudEventFormatter formatter = new JsonEventFormatter();

    public static IEndpointRouteBuilder MapEventsApi( this IEndpointRouteBuilder builder )
    {
        builder.MapPost( "/apis/events", PublishAsync );

        logger = builder.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger( "Apis.Events" );

        return ( builder );
    }

    private static async Task<IResult> PublishAsync( HttpRequest httpRequest, NATSService nats )
    {
        CloudEvent cloudEvent;
        try
        {
            cloudEvent = await httpRequest.ToCloudEventAsync( formatter );
        }
        catch ( ArgumentException )
        {
            return Results.BadRequest();
        }

        // if ( cloudEvent.Type != Constants.FunctionInvokedCloudEventType )
        // {
        //     return Results.UnprocessableEntity();
        // }

        var bytes = formatter.EncodeStructuredModeMessage( cloudEvent, out _ );

        // publish event
        try
        {
            var jetStream = nats.GetConnection()
                .CreateJetStreamContext();

            await jetStream.PublishAsync( cloudEvent.Type, bytes.ToArray() );
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
