using System.Net.Http.Headers;
using System.Text.Json;
using NATS.Client.JetStream;

internal sealed class NATSEventProcessorService : BackgroundService
{
    private readonly ILogger logger;
    private readonly NATSService nats;
    private readonly string gatewayUrl;
    private readonly HttpClient httpClient;
    private readonly TopicMap topicMap = new TopicMap();
    private readonly EventMetrics metrics;

    public NATSEventProcessorService( ILoggerFactory loggerFactory
        , NATSService natsService
        , IHttpClientFactory httpClientFactory
        , EventMetrics eventMetrics
        , Microsoft.Extensions.Options.IOptions<NATSEventProcessorOptions> optionsAccessor )
    {
        logger = loggerFactory.CreateLogger<NATSEventProcessorService>();
        httpClient = httpClientFactory.CreateClient();
        nats = natsService;
        metrics = eventMetrics;

        var options = optionsAccessor.Value;

        gatewayUrl = options.GatewayUrl.TrimEnd( '/' );
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

    private Task ExecuteAsync( NATS.Client.Msg message, CancellationToken cancellationToken )
    {
        message.Ack();

        try
        {
            var faasEvent = JsonSerializer.Deserialize<Event>( message.Data );

            if ( faasEvent == null )
            {
                // TODO: log deserialization error
                return Task.CompletedTask;
            }

            metrics.EventsReceivedTotal( faasEvent.EventType )
                .Inc();

            if ( faasEvent.EventType.Equals( KnownCloudEventTypes.FunctionInvoked ) )
            {
                return InvokeFunction( faasEvent, cancellationToken );
            }

            if ( IsFunctionManagementEvent( faasEvent ) )
            {
                SynchronizeTopicMap( faasEvent );

                return Task.CompletedTask;
            }

            // function topic subscription
            var functionTasks = topicMap.Topics.GetValueOrDefault( faasEvent.EventType )?
                .Select( f => InvokeFunctionWithEvent( f, faasEvent, cancellationToken ) )
                .ToArray();

            if ( functionTasks?.Any() == true )
            {
                return Task.WhenAll( functionTasks );
            }

            // discard unknown types
            return Task.CompletedTask;
        }
        catch ( Exception ex )
        {
            logger.LogError( ex.Message );

            return Task.CompletedTask;
        }
    }

    private bool IsFunctionManagementEvent( Event faasEvent )
        => new string[]
        {
            KnownCloudEventTypes.FunctionAdded,
            KnownCloudEventTypes.FunctionModified,
            KnownCloudEventTypes.FunctionDeleted
        }
        .Contains( faasEvent.EventType );

    private void SynchronizeTopicMap( Event faasEvent )
    {
        if ( !IsFunctionManagementEvent( faasEvent ) )
        {
            return;
        }

        var obj = JsonSerializer.Deserialize<KubernetesObject>( faasEvent.Content );

        if ( obj == null )
        {
            // TODO: log deserialization error
            return;
        }

        var functionName = obj.Metadata?.Name;
        var functionNamespace = obj.Metadata?.Namespace ?? "default";

        if ( functionName == null )
        {
            // TODO: log data error
            return;
        }

        var functionPath = $"{functionNamespace}/{functionName}";

        if ( 
            faasEvent.EventType!.Equals( KnownCloudEventTypes.FunctionAdded )
            ||
            faasEvent.EventType!.Equals( KnownCloudEventTypes.FunctionModified )
        )
        {
            var topics = obj.Metadata?.Annotations?.Where( x => x.Key.Equals( EventAnnotations.EventType ) )
                .SelectMany( x => x.Value.Split( ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries ) )
                .ToArray();

            if ( topics?.Any() == true )
            {
                // update topic map
                topicMap.Subscribe( functionPath, topics );
            }
            else
            {
                // remove function tracking
                topicMap.Unsubscribe( functionPath );
            }
        }

        if ( faasEvent.EventType!.Equals( KnownCloudEventTypes.FunctionDeleted ) == true )
        {
            // remove function tracking
            topicMap.Unsubscribe( functionPath );
        }
    }

    private async Task InvokeFunctionWithEvent( string functionPath, Event faasEvent, CancellationToken cancellationToken )
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
            catch
            {
                // TODO: log content type error
            }
        }

        var httpMessage = new HttpRequestMessage( HttpMethod.Post, $"{gatewayUrl}/proxy/{functionPath}" );

        HttpResponseMessage response;

        logger.LogInformation( $"Start invoking function {functionPath}." );

        try
        {
            response = await httpClient.SendAsync( httpMessage, HttpCompletionOption.ResponseContentRead, cancellationToken );

            logger.LogInformation( $"End invoking function {functionPath}. {(int)response.StatusCode}" );
        }
        catch ( Exception ex )
        {
            logger.LogWarning( $"Failed invoking function {functionPath}. {ex.Message}" );

            return;
        }

        // trigger webhook
        if ( faasEvent.WebhookUrl != null )
        {
            await InvokeWebhookAsync( functionPath, faasEvent.WebhookUrl, response.Content, cancellationToken );
        }
    }

    private async Task InvokeFunction( Event faasEvent, CancellationToken cancellationToken )
    {
        FunctionCall? functionCall;

        try
        {
            functionCall = JsonSerializer.Deserialize<FunctionCall>( faasEvent.Content );
        }
        catch ( Exception )
        {
            // TODO: log deserialization error
            return;
        }

        if ( functionCall == null )
        {
            // unable to deserialize... discard...
            // TODO: log deserialization error
            return;
        }

        var functionPath = string.Join( '/', functionCall.Namespace ?? "default", functionCall.Name );

        var httpMessage = functionCall.ToHttpRequestMessage( functionPath, gatewayUrl );
        HttpResponseMessage response;

        logger.LogInformation( $"Start invoking function {functionPath}." );

        try
        {
            response = await httpClient.SendAsync( httpMessage, HttpCompletionOption.ResponseContentRead, cancellationToken );

            logger.LogInformation( $"End invoking function {functionPath}. {(int)response.StatusCode}" );
        }
        catch ( Exception ex )
        {
            logger.LogWarning( $"Failed invoking functions {functionPath}. {ex.Message}" );

            return;
        }

        // trigger webhook
        if ( faasEvent.WebhookUrl != null )
        {
            await InvokeWebhookAsync( functionPath, faasEvent.WebhookUrl, response.Content, cancellationToken );
        }
    }

    private async Task InvokeWebhookAsync( string functionPath, string webhookUrl, HttpContent httpContent, CancellationToken cancellationToken )
    {
        if ( webhookUrl.StartsWith( "function://" ) )
        {
            webhookUrl = webhookUrl.Replace( "function://", $"{gatewayUrl}/proxy/" );
        }

        logger.LogInformation( $"Start invoking webhook {functionPath} => {webhookUrl}." );

        try
        {
            var response = await httpClient.PostAsync( webhookUrl, httpContent, cancellationToken );

            logger.LogInformation( $"End invoking webhook {functionPath} => {webhookUrl}. {(int)response.StatusCode}" );
        }
        catch ( Exception ex )
        {
            logger.LogWarning( $"Failed invoking webhook {functionPath} => {webhookUrl}. {ex.Message}" );
        }
    }
}
