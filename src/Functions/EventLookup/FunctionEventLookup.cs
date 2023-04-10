using System.Collections.Concurrent;

internal sealed class FunctionEventLookup : IFunctionEventLookup
{
    private readonly ConcurrentDictionary<string,IEnumerable<string>> _functions = new ConcurrentDictionary<string, IEnumerable<string>>();
    private readonly ConcurrentDictionary<string, IEnumerable<string>> _eventTypes = new ConcurrentDictionary<string, IEnumerable<string>>();
    private readonly object _sync = new object();
    private readonly object _map = new object();
    private readonly object _remove = new object();

    public IEnumerable<string> GetEventTypes( string function )
        => _functions.GetValueOrDefault( function ) ?? Enumerable.Empty<string>();

    public IEnumerable<string> GetFunctions( string eventType )
        => _eventTypes.GetValueOrDefault( eventType ) ?? Enumerable.Empty<string>();

    public void Map( string function, IEnumerable<string> eventTypes )
    {
        if ( !eventTypes.Any() )
        {
            return;
        }

        lock ( _map )
        {
            lock ( _sync )
            {
                // track function
                _functions.TryAdd( function, eventTypes );

                foreach ( var t in eventTypes )
                {
                    if ( !_eventTypes.ContainsKey( t ) )
                    {
                        // track new topic with function
                        _eventTypes.TryAdd( t, new string[] { function } );
                    }
                    else
                    {
                        // track topic with function
                        var topicFunctions = _eventTypes[t];

                        _eventTypes[t] = topicFunctions.Append( function )
                            .ToArray();
                    }
                }
            }
        }
    }

    public void Remove( string function )
    {
        lock ( _remove )
        lock ( _sync )
        {
            if ( !_functions.ContainsKey( function ) )
            {
                // function is not tracked
                return;
            }

            // get tracked function topics
            var functionTopics = _functions[function];

            foreach ( var t in functionTopics )
            {
                // exclude function from topic
                var topicFunctions = _eventTypes[t].Where( x => !x.Equals( "name" ) )
                    .ToArray();

                if ( topicFunctions.Any() )
                {
                    // keep topic if tracked by another function
                    _eventTypes[t] = topicFunctions;
                }
                else
                {
                    // remove topic if no one is tracking it
                    _eventTypes.TryRemove( t, out _ );
                }
            }

            _functions.TryRemove( function, out _ );
        }
    }
}
