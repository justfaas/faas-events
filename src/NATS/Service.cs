using NATS.Client.JetStream;

internal sealed class NATSService : IDisposable
{
    private readonly NATS.Client.Options natsOptions;
    private NATS.Client.IConnection? connection;
    public NATSService( ILoggerFactory loggerFactory, Microsoft.Extensions.Options.IOptions<NATSServiceOptions> optionsAccessor )
    {
        var options = optionsAccessor.Value;

        natsOptions = options.NATS;
    }

    public NATS.Client.IConnection GetConnection()
    {
        if ( connection != null )
        {
            return ( connection );
        }

        connection = new NATS.Client.ConnectionFactory()
            .CreateConnection( natsOptions );

        Initialize();

        return ( connection );
    }

    public void Initialize()
    {
        if ( connection == null )
        {
            throw new InvalidOperationException();
        }

        var jetStream = connection.CreateJetStreamManagementContext();

        StreamInfo? streamInfo = null;
        try
        {
            streamInfo = jetStream.GetStreamInfo( "faas-functions" );
        }
        catch {}

        if ( streamInfo == null )
        {
            jetStream.AddStream( 
                StreamConfiguration.Builder()
                    .AddSubjects( KnownCloudEventTypes.FunctionInvoked )
                    .WithName( "faas-functions" )
                    .WithMaxAge( NATS.Client.Internals.Duration.OfHours( 1 ) )
                    .WithRetentionPolicy( RetentionPolicy.WorkQueue )
                    .WithStorageType( StorageType.File )
                    .WithDiscardPolicy( DiscardPolicy.Old )
                    .Build()
            );
        }
    }

    public IJetStreamPullSubscription Subscribe( string subject )
    {
        var natsConnection = GetConnection();
        var jetStream = natsConnection.CreateJetStreamContext();

        var consumerConfiguration = ConsumerConfiguration.Builder()
            .WithAckWait( 2500 )
            .Build();

        var pullOptions = PullSubscribeOptions.Builder()
            .WithDurable( "consumer" )
            .WithConfiguration( consumerConfiguration )
            .Build();

        return jetStream.PullSubscribe( subject, pullOptions );
    }

    public void Dispose()
    {
        connection?.Dispose();
    }
}
