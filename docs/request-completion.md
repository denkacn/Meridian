# Request completion lifecycle

`DataServerNetworkBus.SendRequestAsync<TRes>` completes on the first terminal event:
response, caller cancellation, deadline, send failure, or connection shutdown.
The response claims the request before mapping. A mapping error faults the Task.
Caller cancellation produces a canceled Task with the caller's token. A deadline
produces `TimeoutException`. `CancelPendingRequests()` faults outstanding Tasks
with `IOException` and can be called on disconnect or disposal. `Init()` drains
requests from the previous sender before replacing it.
After `CancelPendingRequests()`, new async requests fail with `IOException`
until `Init()` attaches a sender for the next connection.

Async Task continuations never run inline in response or cancellation callbacks.
Cancellation registrations and timers are disposed by a single queued cleanup
per completed request, outside transport locks. Consumers must call
`CancelPendingRequests()` when their connection is discarded, or link each
request token to their session lifetime. Responses from an earlier `Init()`
cannot complete requests created on a replacement sender because request IDs
are process-unique and pending entries belong to their original bus generation.

The callback overload `SendRequest<TRes>(..., Action<TRes>)` retains its existing
inline callback execution and ordering. It does not use the async completion
path and is not affected by the new cancellation behavior.
