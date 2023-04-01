using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CloudNative.CloudEvents;

internal static class CloudEventExtensions
{
    public static string? GetWebhookUrlAttributeValue( this CloudEvent cloudEvent )
        => cloudEvent.GetPopulatedAttributes()
            .Where( x => x.Key.Name.Equals( "webhookurl" ) )
            .Select( x => x.Value.ToString() )
            .SingleOrDefault();
}
