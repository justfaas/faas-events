using System.Text.Json.Serialization;

public sealed class Event
{
    [JsonPropertyName( "eventType" )]
    public required string EventType { get; set; }

    [JsonPropertyName( "eventSource" )]
    public string? EventSource { get; set; }

    [JsonPropertyName( "content" )]
    public byte[]? Content { get; set; }

    [JsonPropertyName( "contentType" )]
    public string? ContentType { get; set; }

    [JsonPropertyName( "webhookUrl" )]
    public string? WebhookUrl { get; set; }
}
