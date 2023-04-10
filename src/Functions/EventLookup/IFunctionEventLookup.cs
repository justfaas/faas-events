public interface IFunctionEventLookup
{
    /// <summary>
    /// Returns a list of events matching the given function
    /// </summary>
    IEnumerable<string> GetEventTypes( string function );

    /// <summary>
    /// Returns a list of functions matching the given event type
    /// </summary>
    IEnumerable<string> GetFunctions( string eventType );

    /// <summary>
    /// Maps the given event types to a function
    /// </summary>
    void Map( string function, IEnumerable<string> eventTypes );

    /// <summary>
    /// Removes a function; also removes any event type mapping.
    /// </summary>
    void Remove( string function );
}
