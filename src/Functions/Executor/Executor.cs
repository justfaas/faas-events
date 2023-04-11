using Microsoft.Extensions.Options;

internal sealed class FunctionExecutor : IFunctionExecutor
{
    private readonly ILogger logger;
    private readonly HttpClient httpClient;
    private readonly IFunctionEventLookup lookup;
    private readonly string gatewayUrl;

    public FunctionExecutor( 
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IFunctionEventLookup functionEventLookup,
        IOptions<FunctionExecutorOptions> optionsAccessor
    )
    {
        logger = loggerFactory.CreateLogger<FunctionExecutor>();
        httpClient = httpClientFactory.CreateClient();
        lookup = functionEventLookup;

        var options = optionsAccessor.Value;

        gatewayUrl = options.GatewayUrl;
    }

    public Task ExecuteAsync( Event faasEvent, CancellationToken cancellationToken )
    {
        if ( faasEvent.EventType.Equals( KnownCloudEventTypes.FunctionInvoked ) )
        {
            /*
            content is a FunctionCall object
            */
            return ExecuteFunctionCallAsync( faasEvent, cancellationToken );
        }

        /*
        lookup functions matching the event type
        */
        var tasks = lookup.GetFunctions( faasEvent.EventType )
            .Select( f =>
            {
                logger.LogInformation( $"[{faasEvent.EventType}] matched function {f}." );

                return SendHttpMessageAsync(
                    f,
                    faasEvent.UnwrapEvent( gatewayUrl, f ),
                    faasEvent.WebhookUrl,
                    cancellationToken
                );
            } )
            .ToArray();

        if ( tasks?.Any() == true )
        {
            /*
            execute functions
            */
            return Task.WhenAll( tasks );
        }

        logger.LogInformation( $"[{faasEvent.EventType}] has no matches." );

        return Task.CompletedTask;
    }

    private Task ExecuteFunctionCallAsync( Event faasEvent, CancellationToken cancellationToken )
    {
        try
        {
            (string Function, HttpRequestMessage Message) functionCall = faasEvent.UnwrapFunctionCall( gatewayUrl );

            return SendHttpMessageAsync(
                    functionCall.Function,
                    functionCall.Message,
                    faasEvent.WebhookUrl,
                    cancellationToken
                );
        }
        catch ( Exception ex )
        {
            logger.LogError( ex, $"Failed to unwrap function call from event. {ex.Message}" );
        }

        return Task.CompletedTask;
    }

    private async Task SendHttpMessageAsync( string function, HttpRequestMessage httpMessage, string? webhookUrl, CancellationToken cancellationToken )
    {
        HttpResponseMessage response;

        logger.LogInformation( $"Start invoking function {function}." );

        try
        {
            response = await httpClient.SendAsync( httpMessage, HttpCompletionOption.ResponseContentRead, cancellationToken );

            logger.LogInformation( $"End invoking function {function}. {(int)response.StatusCode}" );
        }
        catch ( Exception ex )
        {
            logger.LogWarning( $"Failed invoking functions {function}. {ex.Message}" );

            return;
        }

        // trigger webhook
        if ( webhookUrl != null )
        {
            await InvokeWebhookAsync( function, webhookUrl, response.Content, cancellationToken );
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
