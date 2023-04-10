using k8s;
using Prometheus;

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

builder.Services.AddSingleton<IKubernetes>( provider =>
{
    var config = KubernetesClientConfiguration.IsInCluster()
        ? KubernetesClientConfiguration.InClusterConfig()
        : KubernetesClientConfiguration.BuildConfigFromConfigFile();

    return new Kubernetes( config );
} );

builder.Services.AddSingleton<IFunctionEventLookup, FunctionEventLookup>();
builder.Services.AddHostedService<V1Alpha1FunctionController>();
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

builder.Services.AddSingleton<EventMetrics>();

/*
Kestrel
*/
builder.WebHost.ConfigureKestrel( kestrel =>
{
    var port = 8080;
    #if DEBUG
    if ( int.TryParse( builder.Configuration["PORT"], out var portOverride ) )
    {
        port = portOverride;
    }
    #endif

    kestrel.ListenAnyIP( port );
} );

/*
Runtime
*/
var app = builder.Build();

Metrics.SuppressDefaultMetrics();

app.MapHealthChecks( "/healthz" );
app.UseMetricServer();
app.MapEventsApi();

await app.RunAsync();
