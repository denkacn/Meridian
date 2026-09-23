# Runtime DLL Reload

Server layers are loaded per application DLL. Each layer owns its own TCP server/provider, so reloading one layer does not restart the whole Meridian server.

## Load Model

The server does not load the external application DLL directly from the configured path.

Instead:

1. The provider copies files from the DLL directory into a unique shadow directory under `runtime-layers/<LayerName>/...`.
2. The copied DLL is loaded through a collectible `AssemblyLoadContext`.
3. `MeridianServerLib` is resolved from the host/default context so `IMeridianApplication`, `IServerPeerSession`, and `ServerPeer` keep the same type identity.
4. The provider creates the non-abstract `MeridianApplication` implementation from the shadow DLL.

This keeps the source DLL replaceable while the server is running.

## Reload Flow

When the configured DLL file changes:

1. `FileSystemWatcher` catches `Changed`, `Created`, or `Renamed`.
2. Reload is debounced to avoid reacting to partial copy bursts.
3. The provider waits until the source DLL can be opened for reading.
4. Only this layer's TCP server is stopped.
5. Stopping the layer disconnects all clients connected to that layer.
6. The current application receives `Discard`/`DiscardAsync`.
7. The old application command event is unsubscribed.
8. The old collectible context is unloaded.
9. The new DLL is shadow-copied and loaded.
10. If the layer was running before reload, its TCP server starts again on the same port.

Clients should reconnect normally after the layer is back.

## Compatibility

Old synchronous applications still work through `Setup`, `CreateClient`, and `Discard`.

Async applications can override:

- `SetupAsync`
- `CreateClientAsync`
- `DiscardAsync`

The reload path calls async lifecycle methods when the application implements `IAsyncMeridianApplication`.
