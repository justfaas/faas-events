using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Options;
using Prometheus;

internal class EventMetrics
{
    private readonly ( string Name, string Description ) MetricEventsReceivedTotal
        = ( "faas_events_received_total", "Number of events received." );

    private readonly IReadOnlyDictionary<string, string> metrics;

    private readonly ConcurrentDictionary<string, Counter> counters = new ConcurrentDictionary<string, Counter>();

    public EventMetrics()
    {
        metrics = new Dictionary<string, string>
        {
            { MetricEventsReceivedTotal.Name, MetricEventsReceivedTotal.Description }
        };
    }

    public IEnumerable<string> Names() => metrics.Keys;

    public Counter.Child Counter( string metricName, string eventType )
    {
        if ( !metrics.ContainsKey( metricName ) )
        {
            throw new ArgumentException( "Metric name is not valid!" );
        }

        ( string Name, string Description ) metric = ( metricName, metrics[metricName] );

        return Counter( metric, eventType );
    }

    public Counter.Child EventsReceivedTotal( string eventType )
        => Counter( MetricEventsReceivedTotal, eventType );

    private Counter.Child Counter( ( string Name, string Description ) metric, string eventType )
    {
        if ( !counters.TryGetValue( metric.Name, out var counter ) )
        {
            counter = Metrics.CreateCounter( 
                metric.Name,
                metric.Description,
                new string[]
                {
                    "type",
                }
            );
            
            counters.TryAdd( metric.Name, counter );
        }

        return counter.WithLabels( eventType );
    }
}
