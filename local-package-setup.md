# Local IdWeb Package Setup for MCP

Steps to build local NuGet packages from source (Abstractions + `Id.Web`) and wire them into the MCP build. This is to test local Abstractions changes against the full MCP server build.

## Prerequisites

- Cloned repos side-by-side:
  - `~/code/microsoft-identity-abstractions-for-dotnet`
  - `~/code/microsoft-identity-web`
  - `~/code/mcp`
- Local feed directory: `~/packages/idweb-local`

## 1. Pack Microsoft.Identity.Abstractions

```bash
cd ~/code/microsoft-identity-abstractions-for-dotnet

# Pack (use a distinct version suffix to avoid cache confusion)
dotnet pack src/Microsoft.Identity.Abstractions/Microsoft.Identity.Abstractions.csproj \
  -c Release \
  -o ~/packages/idweb-local \
  /p:Version=11.1.1-no-ext
```

## 2. Point Id.Web at the local Abstractions package

Add the local feed to Id.Web's `NuGet.config`:

```xml
<packageSources>
  <add key="idweb-local" value="/Users/<you>/packages/idweb-local" />
  <!-- keep existing sources below -->
</packageSources>
```

Update Id.Web's `Directory.Packages.props` to reference the local version:

```xml
<PackageVersion Include="Microsoft.Identity.Abstractions" Version="11.1.1-no-ext" />
```

## 3. Pack `Id.Web` and all transitive dependencies

Each package that MCP transitively pulls in must be packed with a matching version suffix:

```bash
cd ~/code/microsoft-identity-web
VERSION=4.4.0-no-ext
OUT=~/packages/idweb-local

dotnet pack src/Microsoft.Identity.Web/Microsoft.Identity.Web.csproj                       -c Release -o $OUT /p:Version=$VERSION
dotnet pack src/Microsoft.Identity.Web.Azure/Microsoft.Identity.Web.Azure.csproj           -c Release -o $OUT /p:Version=$VERSION
dotnet pack src/Microsoft.Identity.Web.Certificate/Microsoft.Identity.Web.Certificate.csproj -c Release -o $OUT /p:Version=$VERSION
dotnet pack src/Microsoft.Identity.Web.TokenCache/Microsoft.Identity.Web.TokenCache.csproj -c Release -o $OUT /p:Version=$VERSION
dotnet pack src/Microsoft.Identity.Web.TokenAcquisition/Microsoft.Identity.Web.TokenAcquisition.csproj -c Release -o $OUT /p:Version=$VERSION
dotnet pack src/Microsoft.Identity.Web.Diagnostics/Microsoft.Identity.Web.Diagnostics.csproj -c Release -o $OUT /p:Version=$VERSION
dotnet pack src/Microsoft.Identity.Web.Certificateless/Microsoft.Identity.Web.Certificateless.csproj -c Release -o $OUT /p:Version=$VERSION
```

## 4. Configure MCP to use local packages

### NuGet.config

Add the local feed **before** nuget.org:

```xml
<packageSources>
  <add key="idweb-local" value="/Users/anuchandy/packages/idweb-local" />
  <!-- existing sources -->
</packageSources>
```

### Directory.Packages.props

Update the versions:

```xml
<PackageVersion Include="Microsoft.Identity.Abstractions" Version="11.1.1-no-ext" />
<PackageVersion Include="Microsoft.Identity.Web" Version="4.4.0-no-ext" />
<PackageVersion Include="Microsoft.Identity.Web.Azure" Version="4.4.0-no-ext" />
```

### Directory.Build.props (temporary)

Suppress the NU1605 downgrade error (Id.Web internals still reference the original Abstractions version):

```xml
<NoWarn>$(NoWarn);NU1605</NoWarn>
```

## 5. Build MCP

```bash
cd ~/code/mcp
pwsh -Command "./eng/scripts/Build-Code.ps1 \
  -BuildInfoPath ./.work/build_info.json \
  -PlatformName 'macos-arm64' \
  -SelfContained -SingleFile -ReleaseBuild \
  -ServerName 'Azure.Mcp.Server'"
```

## Cleanup

To revert MCP back to upstream packages:

1. Remove `idweb-local` from `NuGet.config`
2. Restore original versions in `Directory.Packages.props`
3. Remove `NU1605` from `Directory.Build.props`
4. `dotnet nuget locals all --clear` (optional, clears cached local packages)
