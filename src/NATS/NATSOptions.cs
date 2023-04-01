using NATS.Client;

public sealed class NATSServiceOptions
{
    public NATSServiceOptions()
    {
        NATS.Url = "nats://nats:4222";
    }

    public Options NATS { get; } = ConnectionFactory.GetDefaultOptions();
}
