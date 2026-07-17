# lod.client

This project contains the Kiota-generated C# client library for the LOD API.

## Regenerating the client

The client is generated from `..\..\docs\swagger.json`.
The generated Kiota files are written under the project-local `Client/` folder so the project layout stays tidy.

To update the generated code after the Swagger definition changes, run from the repository root:

```powershell
cd lod-mcp-dotnet
.\scripts\update-lod-client.ps1
```

If the `kiota` CLI is not installed, install it globally:

```powershell
dotnet tool install -g microsoft.openapi.kiota
```

If the Swagger document adds or removes operations, run the script again to refresh the generated client and lock file.
