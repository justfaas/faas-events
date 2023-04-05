using System.Text.Json.Serialization;

internal sealed class KubernetesObject
{
    [JsonPropertyName( "metadata" )]
    public MetadataObject? Metadata { get; set; }
}

internal sealed class MetadataObject
{
    [JsonPropertyName( "name" )]
    public string? Name { get; set; }

    [JsonPropertyName( "namespace" )]
    public string? Namespace { get; set; }

    [JsonPropertyName( "annotations" )]
    public Dictionary<string, string>? Annotations { get; set; }
}
