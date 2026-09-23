# Meridian Test Logic

This repository contains a small test layer and command-line client for checking Meridian transport and `MeridianRequestSystem` request routing.

## Projects

- `MeridianTestContracts` - shared request and response contracts.
- `MeridianTestLogic` - external Meridian DLL loaded by `MeridianServer`.
- `MeridianTestClient` - interactive command-line client.

## Server Setup

Build the test logic:

```powershell
dotnet build .\MeridianTestLogic\MeridianTestLogic.csproj
```

Copy the test DLL files to a dedicated `MeridianTest` folder near `MeridianServer.dll`:

```powershell
New-Item -ItemType Directory -Force .\MeridianServer\bin\Debug\net8.0-windows7.0\MeridianTest
Copy-Item -Force `
  .\MeridianTestLogic\bin\Debug\netstandard2.1\MeridianTestLogic.dll,`
  .\MeridianTestLogic\bin\Debug\netstandard2.1\MeridianTestContracts.dll,`
  .\MeridianTestLogic\bin\Debug\netstandard2.1\MeridianRequestSystem.dll `
  .\MeridianServer\bin\Debug\net8.0-windows7.0\MeridianTest\
```

Point `MeridianServer` `server_setting.json` to the copied DLL:

```json
{
  "Layers": [
    {
      "LayerName": "MeridianTest",
      "PathToExternalApplicationLib": "MeridianTest\\MeridianTestLogic.dll",
      "Port": 4555
    }
  ],
  "IsDebugEnable": true,
  "IsAutoStart": true
}
```

The path is resolved relative to the `MeridianServer` output directory.

## Client

Run the client after the server starts:

```powershell
dotnet run --project .\MeridianTestClient\MeridianTestClient.csproj -- 127.0.0.1 4555
```

Available commands:

```text
ping
echo hello meridian
sum 2 3
exit
```
