# Meridian DLL Message Flow

This document describes what a Meridian external DLL receives from the transport layer and how message handling should be implemented.

## Packet Format

Network packets use TCP stream framing. A single TCP receive can contain a partial packet, one full packet, or several packets. The transport layer handles this before data reaches the DLL.

Current protocol header size: 12 bytes.

```text
bytes 0-1   magic: 'M' 'R'
byte 2      protocol version: 1
byte 3      flags: 0
bytes 4-7   messageId, int32 little-endian
bytes 8-11  payloadLength, int32 little-endian
bytes 12..  MessagePack payload
```

The payload is a MessagePack-serialized `OperationData`.

```csharp
[MessagePackObject]
public readonly struct OperationData
{
    [Key(0)]
    public byte OperationCode { get; }

    [Key(1)]
    public Dictionary<byte, object> Parameters { get; }
}
```

## Server Receive Flow

The DLL does not receive raw TCP bytes. The server transport does this first:

```text
TCP bytes
-> HeaderSocketMessageComponatorV3
-> MessagePack deserialize
-> OperationData
-> per-peer incoming queue
-> DLL handler
```

Each peer has its own incoming queue. Messages for one peer are processed sequentially and in receive order.

```text
operation 1 -> await handler
operation 2 -> await handler
operation 3 -> await handler
```

## Implementing Message Handling In DLL

For new DLL code, prefer async handling by overriding `OnReceivedMessageAsync` in your `ServerPeer` subclass:

```csharp
protected override async Task OnReceivedMessageAsync(
    object sender,
    OperationData messageData,
    CancellationToken cancellationToken)
{
    await HandleOperationAsync(messageData, cancellationToken);
}
```

The next message from the same peer will not be processed until this method finishes.

Old synchronous handlers are still supported:

```csharp
protected override void OnReceivedMessage(object sender, OperationData messageData)
{
    HandleOperation(messageData);
}
```

The default async implementation calls the old sync method, so existing DLLs remain compatible.

## Application Lifecycle

External applications can also implement async lifecycle logic by overriding these methods in `MeridianApplication`:

```csharp
protected override async Task SetupAsync(
    string id,
    string path,
    CancellationToken cancellationToken)
{
    await InitializeAsync(cancellationToken);
}

protected override async Task<ServerPeer> CreateClientAsync(
    IServerPeerSession peerSession,
    CancellationToken cancellationToken)
{
    await PrepareClientAsync(cancellationToken);
    return new MyServerPeer(peerSession);
}

protected override async Task DiscardAsync(CancellationToken cancellationToken)
{
    await ShutdownAsync(cancellationToken);
}
```

Synchronous `Setup`, `CreateClient`, and `Discard` are still supported for legacy DLLs.

## Cancellation And Errors

When a client disconnects or the session is disposed:

- the peer cancellation token is cancelled;
- the incoming queue is closed;
- pending async handlers should stop if they observe the token.

DLL handler exceptions are caught and logged by the transport layer. One failed handler should not crash the socket receive loop.

## Important Notes

- Do not parse TCP headers in the DLL.
- Do not assume one socket receive equals one operation.
- Use `OperationCode` to route business logic.
- Use `Parameters` for operation payload data.
- Prefer async handlers for IO, timers, file access, database calls, HTTP calls, and long-running work.
- Keep per-peer ordering assumptions local to one peer. Different peers are processed independently.
