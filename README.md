# FaaS Events

This component reads events from a stream source. Some of these events are known by the component and as such, dealt by it directly. All other events go through topic matching for function invocation.

## Known event types

All known event types have the `com.justfaas.` prefix. They are intended for the component.

| Event type             | Description                                                                |
|----------------------- | -------------------------------------------------------------------------- |
| function.invoked       | A function call event. Data contains details to invoke a function.         |
| function.added         | Sent when a function is added/created. Used for event type mappings.       |
| function.modified      | Sent when a function is modified. Used for event type mappings.            |
| function.deleted       | Sent when a function is deleted. Used for event type mappings.             |
| event.added            | Sent when an event is added. Triggers function(s) matching the event type. |


## API

The component also exposes an api to allow publishing events from other sources. This is how the gateway publishes async requests, but it can also be used to create connectors from other platforms.

```
POST /apis/events
```

The content of the POST request is the event data. Other event details are set using headers.

| Header              | Description                                                                       |
|-------------------- | ---------------------------------------------------------------------------------- |
| Content-Type        | Optional. The event data content type.                                             |
| X-Event-Source      | Optional. Used to indicate the event source.                                       |
| X-Event-Type        | Required. Indicates the type of the event.                                         |
| X-Event-Webhook-Url | Optional. When event triggers a function, this is used to indicate a callback Url. |

## Management events

The operator is actively watching functions in the cluster. When a function is reconciled or deleted, a management event is sent with the `com.justfaas.function.added|modified|deleted` type. The event data will contain the function resource object, but only the metadata, without the spec - in other words, a generic object resource. 

This allows the component to map and keep track of function-subscribed topics for triggering functions when a topic matches an event.
