using System.Net.Http.Headers;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;
using NATS.Client.JetStream;

internal sealed class NATSEventProcessorService : BackgroundService
{
    private readonly ILogger logger;
    private readonly CloudEventFormatter formatter = new JsonEventFormatter();
    private readonly NATSService nats;
    private readonly string gatewayUrl;
    private readonly HttpClient httpClient;
    private readonly TopicMap topicMap = new TopicMap();

    public NATSEventProcessorService( ILoggerFactory loggerFactory
        , NATSService natsService
        , IHttpClientFactory httpClientFactory
        , Microsoft.Extensions.Options.IOptions<NATSEventProcessorOptions> optionsAccessor )
    {
        logger = loggerFactory.CreateLogger<NATSEventProcessorService>();
        httpClient = httpClientFactory.CreateClient();
        nats = natsService;

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
                //subscription = nats.Subscribe( Constants.FunctionInvokedCloudEventType );
                subscription = nats.Subscribe( ">" );

                //logger.LogInformation( $"Subscribed to '{Constants.CloudEventType}' events." );
                logger.LogInformation( $"Listening." );
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
            var cloudEvent = message.ToCloudEvent( formatter );

            if ( cloudEvent.Type == null )
            {
                // event type is required
                return Task.CompletedTask;
            }

            if ( cloudEvent.Type.Equals( KnownCloudEventTypes.FunctionInvoked ) )
            {
                return InvokeFunction( cloudEvent, cancellationToken );
            }

            if ( IsFunctionManagementEvent( cloudEvent ) )
            {
                SynchronizeTopicMap( cloudEvent );

                return Task.CompletedTask;
            }

            // function topic subscription
            var functionTasks = topicMap.Topics.GetValueOrDefault( cloudEvent.Type )?
                .Select( f => InvokeFunctionWithEvent( f, cloudEvent, cancellationToken ) )
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

    private bool IsFunctionManagementEvent( CloudEvent cloudEvent )
    {
        if ( ( cloudEvent.Type == null ) || ( cloudEvent.Subject == null ) )
        {
            return ( false );
        }

        return new string[]
        {
            KnownCloudEventTypes.FunctionAdded,
            KnownCloudEventTypes.FunctionModified,
            KnownCloudEventTypes.FunctionDeleted
        }
        .Contains( cloudEvent.Type );
    }

    private void SynchronizeTopicMap( CloudEvent cloudEvent )
    {
        if ( !IsFunctionManagementEvent( cloudEvent ) )
        {
            return;
        }

        if ( 
            cloudEvent.Type!.Equals( KnownCloudEventTypes.FunctionAdded )
            ||
            cloudEvent.Type!.Equals( KnownCloudEventTypes.FunctionModified )
        )
        {
            var obj = cloudEvent.ReadDataAsKubernetesObject();

            var topics = obj?.Metadata?.Annotations?.Where( x => x.Key.Equals( "justfaas.com/topic" ) )
                .SelectMany( x => x.Key.Split( ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries ) )
                .ToArray();

            if ( topics?.Any() == true )
            {
                // update topic map
                topicMap.Subscribe( cloudEvent.Subject!, topics );
            }
            else
            {
                // remove function tracking
                topicMap.Unsubscribe( cloudEvent.Subject! );
            }
        }

        if ( cloudEvent.Type!.Equals( KnownCloudEventTypes.FunctionDeleted ) == true )
        {
            // remove function tracking
            topicMap.Unsubscribe( cloudEvent.Subject! );
        }
    }

    private async Task InvokeFunctionWithEvent( string functionPath, CloudEvent cloudEvent, CancellationToken cancellationToken )
    {
        var bytes = formatter.EncodeStructuredModeMessage( cloudEvent, out var contentType );

        var httpContent = new ByteArrayContent(
            bytes.ToArray()
        );


        httpContent.Headers.ContentType = new MediaTypeHeaderValue( contentType.MediaType, contentType.CharSet );

        var httpMessage = new HttpRequestMessage( HttpMethod.Post, $"{gatewayUrl}/proxy/{functionPath}" );

        HttpResponseMessage response;

        logger.LogInformation( $"Start invoking functions/{cloudEvent.Subject}." );

        try
        {
            response = await httpClient.SendAsync( httpMessage, HttpCompletionOption.ResponseContentRead, cancellationToken );

            logger.LogInformation( $"End invoking functions/{cloudEvent.Subject}. {(int)response.StatusCode}" );
        }
        catch ( Exception ex )
        {
            logger.LogWarning( $"Failed invoking functions/{cloudEvent.Subject}. {ex.Message}" );

            return;
        }

        // trigger webhook
        var webhookValue = cloudEvent.GetWebhookUrlAttributeValue();

        if ( webhookValue != null )
        {
            await InvokeWebhookAsync( functionPath, webhookValue, response.Content, cancellationToken );
        }
    }

    private async Task InvokeFunction( CloudEvent cloudEvent, CancellationToken cancellationToken )
    {
        if ( cloudEvent.Subject == null )
        {
            logger.LogError( "Unable to invoke function. Subject has a null value." );

            return;
        }

        FunctionCall? functionCall;

        try
        {
            functionCall = cloudEvent.ToFunctionCall();
        }
        catch ( Exception )
        {
            // TODO: log
            return;
        }

        if ( functionCall == null )
        {
            // unable to deserialize... discard...
            // TODO: log?
            return;
        }

        var httpMessage = functionCall.ToHttpRequestMessage( cloudEvent.Subject, gatewayUrl );
        HttpResponseMessage response;

        logger.LogInformation( $"Start invoking functions/{cloudEvent.Subject}." );

        try
        {
            response = await httpClient.SendAsync( httpMessage, HttpCompletionOption.ResponseContentRead, cancellationToken );

            logger.LogInformation( $"End invoking functions/{cloudEvent.Subject}. {(int)response.StatusCode}" );
        }
        catch ( Exception ex )
        {
            logger.LogWarning( $"Failed invoking functions/{cloudEvent.Subject}. {ex.Message}" );

            return;
        }

        // trigger webhook
        var webhookValue = cloudEvent.GetWebhookUrlAttributeValue();

        if ( webhookValue != null )
        {
            await InvokeWebhookAsync( cloudEvent.Subject, webhookValue, response.Content, cancellationToken );
        }
    }

    private async Task InvokeWebhookAsync( string subject, string webhookUrl, HttpContent httpContent, CancellationToken cancellationToken )
    {
        if ( webhookUrl.StartsWith( "function://" ) )
        {
            webhookUrl = webhookUrl.Replace( "function://", $"{gatewayUrl}/proxy/" );
        }

        logger.LogInformation( $"Start invoking webhooks/{subject}." );

        try
        {
            var response = await httpClient.PostAsync( webhookUrl, httpContent, cancellationToken );

            logger.LogInformation( $"End invoking webhooks/{subject}. {(int)response.StatusCode}" );
        }
        catch ( Exception ex )
        {
            logger.LogWarning( $"Failed invoking webhooks/{subject}. {ex.Message}" );
        }
    }
}
