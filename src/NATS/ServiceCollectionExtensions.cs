internal static class NATSServiceCollectionExtensions
{
    public static IServiceCollection AddNATS( this IServiceCollection services, Action<NATSServiceOptions> configure )
    {
        services.AddSingleton<NATSService>()
            .Configure( configure );

        services.AddHealthChecks()
            .AddCheck<NATSHealthCheck>( "NATS"
                , Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy
                , new string[] { "nats" } );

        return ( services );
    }
}
