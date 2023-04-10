using k8s;
using k8s.Models;

internal sealed class V1Alpha1FunctionController : KubeController<V1Alpha1Function>
{
    private readonly ILogger logger;
    private readonly IFunctionEventLookup lookup;

    public V1Alpha1FunctionController( 
        ILoggerFactory loggerFactory,
        IKubernetes kubernetesClient,
        IFunctionEventLookup functionEventLookup
    )
        : base( loggerFactory, kubernetesClient )
    {
        logger = loggerFactory.CreateLogger<V1Alpha1FunctionController>();
        lookup = functionEventLookup;

        // TODO: should we move the event-type annotation to a label?
        // the advantage would be that we could use a label selector here
        // to only watch for resources containing an event-type label.
        // The downside is that the user has to remember that unlike other
        // "modifiers", this one needs to be set as a label instead of an annotation.
    }

    protected override Task DeletedAsync( V1Alpha1Function function )
    {
        var eventType = function.GetAnnotation( EventAnnotations.EventType );

        if ( eventType != null )
        {
            lookup.Remove( function.NamespacedName() );

            logger.LogInformation( $"Function {function.NamespacedName()} dropped all bindings." );
        }

        return Task.CompletedTask;
    }

    protected override Task ReconcileAsync( V1Alpha1Function function )
    {
        var eventType = function.GetAnnotation( EventAnnotations.EventType );

        // since we dont' know if we had mappings before... we attempt to remove the function nonetheless...
        // if we were using labels instead of annotations, we would only get a reconcile when label exists
        // this would save an "empty" reconcile call
        var previousEventTypes = lookup.GetEventTypes( function.NamespacedName() );

        if ( previousEventTypes.Any() )
        {
            lookup.Remove( function.NamespacedName() );

            var previousEventType = string.Join( ',', previousEventTypes );

            logger.LogInformation( $"Function {function.NamespacedName()} dropped binding(s) [{previousEventType}]." );
        }

        if ( eventType != null )
        {
            var eventTypes = function.Annotations()
                .Where( x => x.Key.Equals( EventAnnotations.EventType ) )
                .SelectMany( x => x.Value.Split( ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries ) )
                .ToArray();

            lookup.Map( function.NamespacedName(), eventTypes );

            logger.LogInformation( $"Function {function.NamespacedName()} added binding(s) [{eventType}]." );
        }

        return Task.CompletedTask;
    }
}
