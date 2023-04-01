using CloudNative.CloudEvents;

internal static class NATSCloudEventExtensions
{
    public static CloudEvent ToCloudEvent( this NATS.Client.Msg message, CloudEventFormatter formatter )
        => formatter.DecodeStructuredModeMessage( message.Data, null, null );
}
