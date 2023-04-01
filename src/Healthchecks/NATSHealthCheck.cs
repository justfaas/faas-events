using Microsoft.Extensions.Diagnostics.HealthChecks;
using NATS.Client;

internal sealed class NATSHealthCheck : IHealthCheck
{
    private readonly NATSService nats;

    public NATSHealthCheck( NATSService natsService )
    {
        nats = natsService;
    }

    public Task<HealthCheckResult> CheckHealthAsync( HealthCheckContext context, CancellationToken cancellationToken = default )
    {
        try
        {
            var natsConnection = nats.GetConnection();

            return natsConnection.State switch
            {
                ConnState.CONNECTED
                    => Task.FromResult( HealthCheckResult.Healthy() ),
                ConnState.CONNECTING or ConnState.RECONNECTING or ConnState.DRAINING_PUBS or ConnState.DRAINING_SUBS
                    => Task.FromResult( HealthCheckResult.Degraded() ),
                ConnState.CLOSED or ConnState.DISCONNECTED
                    => Task.FromResult( HealthCheckResult.Unhealthy() ),
                _
                    => Task.FromResult( context.Fail() )
            };
        }
        catch ( Exception ex )
        {
            return Task.FromResult( context.Fail( ex.Message, ex ) );
        }
    }
}
