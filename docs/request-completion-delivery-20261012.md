# Request completion delivery (2026-10-12)

## Library artifact

- Package/file version: `3.0.6` / `3.0.6.0`.
- Farwake-compatible assembly identity: `MeridianRequestSystem, Version=3.0.4.0`.
- SHA-256: `97E301162B50D4FCF2FD24947E18457818217CF269DB895FBAEE23072DD79509`.
- The same hash is pinned in `Backend/Libs`, the Unity plugin directory, and the rebuilt JourneyClient and LoadClient outputs.
- The existing Farwake client transport remains `MeridianServerLib, Version=3.0.4.0`, file version `3.0.6.0`, SHA-256 `E808C34CC53F3C7434163B1BE4543C7476D8620BC07A248B1FF7345168AC5719`.

## Local verification

- `MeridianRequestSystem` Release build for `netstandard2.1`: passed.
- `MeridianTestClient --request-completion-checks`: passed.
- The checks cover immediate response, already-canceled tokens, cancellation during registration and response mapping, timeout and late response, send/mapping failures, disconnect during send, continuation-triggered disconnect, replacement isolation, and 100 request lifecycle cycles with zero retained pending entries.
- `Farwake.Client`, `Farwake.JourneyClient`, and `Farwake.LoadClient` Release builds: passed with zero warnings and errors.
- Wire fields and operation codes were not changed.

## Deployment boundary

No running host was replaced or restarted. No remote soak was started. The Farwake server-side transport DLL was not changed. Local SDK pins and client build outputs are not a deployment.

The Meridian build reported existing NuGet advisories for `MessagePack 3.1.4` (`NU1902` and `NU1903`). These are separate from request completion.
