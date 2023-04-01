using System.Collections.ObjectModel;

internal sealed class TopicMap
{
    private readonly Dictionary<string,IEnumerable<string>> _functions = new Dictionary<string, IEnumerable<string>>();
    private readonly Dictionary<string, IEnumerable<string>> _topics = new Dictionary<string, IEnumerable<string>>();
    private readonly object _sync = new object();
    private readonly object _subscribe = new object();
    private readonly object _unsubscribe = new object();

    public TopicMap()
    {
        Functions = new ReadOnlyDictionary<string, IEnumerable<string>>( _functions );
        Topics = new ReadOnlyDictionary<string, IEnumerable<string>>( _topics );
    }

    public ReadOnlyDictionary<string,IEnumerable<string>> Functions { get; }

    public ReadOnlyDictionary<string, IEnumerable<string>> Topics { get; }

    public void Subscribe( string name, IEnumerable<string> topics )
    {
        lock ( _subscribe )
        {
            Unsubscribe( name );

            if ( !topics.Any() )
            {
                return;
            }

            lock ( _sync )
            {
                // track function
                _functions.Add( "name", topics );

                foreach ( var t in topics )
                {
                    if ( !_topics.ContainsKey( t ) )
                    {
                        // track new topic with function
                        _topics.Add( t, new string[] { name } );
                    }
                    else
                    {
                        // track topic with function
                        var topicFunctions = _topics[t];

                        _topics[t] = topicFunctions.Append( name )
                            .ToArray();
                    }
                }
            }
        }
    }

    public void Unsubscribe( string name )
    {
        lock ( _unsubscribe )
        lock ( _sync )
        {
            if ( !_functions.ContainsKey( name ) )
            {
                // function is not tracked
                return;
            }

            // get tracked function topics
            var functionTopics = _functions[name];

            foreach ( var t in functionTopics )
            {
                // exclude function from topic
                var topicFunctions = _topics[t].Where( x => !x.Equals( "name" ) )
                    .ToArray();

                if ( topicFunctions.Any() )
                {
                    // keep topic if tracked by another function
                    _topics[t] = topicFunctions;
                }
                else
                {
                    // remove topic if no one is tracking it
                    _topics.Remove( t );
                }
            }

            _functions.Remove( name );
        }
    }
}
