$ErrorActionPreference = 'Stop'
$sdk = Join-Path $PSScriptRoot '..\DiskTrace\.dotnet\dotnet.exe'
if (-not (Test-Path $sdk)) {
    $sdk = 'dotnet'
}

& $sdk publish (Join-Path $PSScriptRoot 'AppKeeper.csproj') `
    -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -o (Join-Path $PSScriptRoot 'publish\win-x64') --nologo
