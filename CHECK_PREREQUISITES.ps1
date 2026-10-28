$ErrorActionPreference = 'Stop'
$required = @('RengaCOMAPI.tlb', 'Renga.NET8.PluginUtility.dll')
foreach ($name in $required) {
  $file = Join-Path $PSScriptRoot "sdk\$name"
  if (!(Test-Path -LiteralPath $file -PathType Leaf)) {
    throw "Required local SDK file is missing: $name. See sdk/README.md and run PREPARE_SDK.ps1."
  }
  Write-Host "OK: $name" -ForegroundColor Green
}
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {throw 'dotnet not found; install .NET 8 SDK / Desktop Development workload.'}
Write-Host 'SDK files found. To compile COM interop on Windows use Visual Studio Developer PowerShell / MSBuild.'
