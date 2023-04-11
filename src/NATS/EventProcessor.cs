using System.Net.Http.Headers;
using System.Text.Json;
using NATS.Client.JetStream;

internal sealed class NATSEventProcessorService : BackgroundService
{
    private readonly ILogger logger;
    private readonly NATSService nats;
    private readonly EventMetrics metrics;
    private readonly IFunctionExecutor executor;

    public NATSEventProcessorService( ILoggerFactory loggerFactory
        , NATSService natsService
        , IFunctionExecutor functionExecutor
        , EventMetrics eventMetrics
    )
    {
        logger = loggerFactory.CreateLogger<NATSEventProcessorService>();
        nats = natsService;
        metrics = eventMetrics;
        executor = functionExecutor;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation( "Started." );

        return base.StartAsync( cancellationToken );
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation( "Stopped." );

        return base.StopAsync( cancellationToken );
    }

    protected override async Task ExecuteAsync( CancellationToken stoppingToken )
    {
        IJetStreamPullSubscription? subscription = null;
        while ( ( subscription == null ) && !stoppingToken.IsCancellationRequested )
        {
            try
            {
                subscription = nats.Subscribe( "com.justfaas.>" );

                logger.LogInformation( $"Subscribed to 'com.justfaas.>' event types." );
            }
            catch ( Exception ex )
            {
                logger.LogError( ex.Message );

                await Task.Delay( 5000, stoppingToken );
            }
        }

        while ( !stoppingToken.IsCancellationRequested )
        {
            var messages = subscription!.Fetch( 10, 1000 );

            if ( messages.Any() == true )
            {
                var tasks = messages.Select( msg => ExecuteAsync( msg, stoppingToken ) );

                await Task.WhenAll( tasks );
                continue;
            }

            await Task.Delay( 1000, stoppingToken );
        }

        subscription?.Unsubscribe();
    }

    private async Task ExecuteAsync( NATS.Client.Msg message, CancellationToken cancellationToken )
    {
        message.Ack();

        try
        {
            var faasEvent = JsonSerializer.Deserialize<Event>( message.Data );

            if ( faasEvent == null )
            {
                logger.LogWarning( "Failed to deserialize event." );

                return;
            }

            metrics.EventsReceivedTotal( faasEvent.EventType )
                .Inc();

            await executor.ExecuteAsync( faasEvent, cancellationToken );
        }
        catch ( Exception ex )
        {
            logger.LogError( ex, ex.Message );
        }
    }
}
