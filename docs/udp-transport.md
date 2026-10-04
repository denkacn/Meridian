# UDP transport

TCP remains the default. The client opts into UDP after its TCP connection is established:

```csharp
var client = new ClientPeer(host, port, logger);
client.ClientConnected += () => client.EnableUdp();
client.Connect();
```

The server opens UDP on the same numeric port as TCP. It sends a session token over TCP; the client uses that token to bind its UDP endpoint to the existing `ServerPeerSession`. If UDP is unavailable, the client can continue using TCP.

Server logic must explicitly allow each incoming UDP operation:

```csharp
var peer = new MyServerPeer(peerSession);
peer.AllowUdpOperation(MoveOperationCode);
return peer;
```

Send an operation using either delivery mode:

```csharp
await client.SendUdpAsync(moveOperation, UdpDeliveryMode.Unreliable);
bool acknowledged = await client.SendUdpAsync(eventOperation, UdpDeliveryMode.Reliable);
```

`Reliable` retransmits up to five total attempts at 250 ms intervals. `true` means that the receiver acknowledged enqueueing the operation; it does not mean the application successfully executed it. Failed acknowledgements return `false`. Do not retry a non-idempotent application operation blindly after a timeout: its ACK may have been lost.

Both transports decode to `OperationData` and use the same server peer callbacks. Request/response operations remain on TCP; do not allow their operation codes over UDP. Operation code `255` is reserved for UDP negotiation. A UDP datagram is limited to 1200 bytes including its 24-byte header. When UDP has not finished binding, `SendUdpAsync` sends through TCP. No encryption or ordering guarantee is provided.
