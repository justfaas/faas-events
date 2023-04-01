# FaaS Events

This component reads [CloudEvents](https://cloudevents.io/) from a stream source. These events are expected to be function invocation requests, as as such, the component attempts to invoke the function through the gateway.

The component also exposes an api to allow publishing events from other sources. This is how the gateway publishes async requests, but it can also be used to create connectors from other platforms.

The `type` value must be `com.justfaas.function.invoked` and the `subject` must be the namespaced function name.

Sample event sent from the gateway

```json
{
    "specversion" : "1.0",
    "type" : "com.justfaas.function.invoked",
    "source" : "http://gateway.faas.svc.cluster.local:8080/cloudevents/spec/function",
    "subject" : "default/hello",
    "id" : "GRuGfejeRkjpQrtBgHs3VA",
    "time" : "2023-03-18T18:31:00Z",
}
```

## Supported data types

The event data type is expected to be of type `String`, `Byte[]` or serializable as a `JsonElement` type. Anything else will result in a not supported exception and the function not being invoked.

## Webhooks

To define a WebHook to be invoked after the function request completes, the extended attribute `webhookurl` should be used. Here's an example to invoke another function *callback* in the *default* namespace on completion.

```json
{
    "specversion" : "1.0",
    "type" : "com.justfaas.function.invoked",
    "source" : "http://gateway.faas.svc.cluster.local:8080/cloudevents/spec/function",
    "subject" : "default/hello",
    "id" : "GRuGfejeRkjpQrtBgHs3VA",
    "time" : "2023-03-18T18:31:00Z",
    "webhookurl": "http://gateway.faas.svc.cluster.local:8080/proxy/default/callback"
}
```

If what we want to invoke is another function in the cluster (as in the example above), we can just indicate the name (including namespace if not default) of the function, using the `function` scheme.

```json
{
    "specversion" : "1.0",
    "type" : "com.justfaas.function.invoked",
    "source" : "http://gateway.faas.svc.cluster.local:8080/cloudevents/spec/function",
    "subject" : "default/hello",
    "id" : "GRuGfejeRkjpQrtBgHs3VA",
    "time" : "2023-03-18T18:31:00Z",
    "webhookurl": "function://default/callback"
}
```

## Function Event Delivery

The operator is actively watching functions in the cluster. When a function is reconciled or deleted, an event is sent with the `com.justfaas.function.added|modified|deleted` type. The subject will contain the target function path and the data, a resource object containing only the metadata. This allows this component to map and keep track of function-subscribed topics and to triggering functions when a topic matches.
