using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/*
Builder
*/
var builder = WebApplication.CreateBuilder( args );

#if DEBUG
builder.Configuration.AddJsonFile( "config.json", optional: true, reloadOnChange: false );
#endif

builder.Logging.ClearProviders()
    .AddSimpleConsole( options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    } )
    .AddFilter( "Microsoft.AspNetCore.Http.Result", LogLevel.Warning )
    .AddFilter( "Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.Warning )
    .AddFilter( "System.Net.Http.HttpClient.Default.ClientHandler", LogLevel.Warning );

builder.Services.AddHealthChecks();
builder.Services.AddHostedService<NATSEventProcessorService>()
    #if DEBUG
    .Configure<NATSEventProcessorOptions>( options =>
    {
        var gatewayUrl = builder.Configuration["GATEWAY_URL"];

        if ( gatewayUrl != null )
        {
            options.GatewayUrl = gatewayUrl;
        }
    } )
    #endif
    .AddHttpClient()
    .AddNATS( options =>
    {
        #if DEBUG
        var natsUrl = builder.Configuration["NATS_URL"];

        if ( natsUrl != null )
        {
            options.NATS.Url = natsUrl;
        }
        #endif
    } );

/*
Kestrel
*/
builder.WebHost.ConfigureKestrel( kestrel =>
{
    kestrel.ListenAnyIP( 8080 );
} );

/*
Runtime
*/
var app = builder.Build();

app.MapHealthChecks( "/healthz" );
app.MapEventsApi();

await app.RunAsync();
