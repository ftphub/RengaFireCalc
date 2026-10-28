# Imports two required SDK files from an official SDK ZIP. Never commit these vendor binaries.
param([Parameter(Mandatory=$true)][string]$SdkZip)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $SdkZip -PathType Leaf)) { throw "SDK archive not found: $SdkZip" }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $SdkZip).Path)
try {
  $files = @('RengaCOMAPI.tlb', 'Renga.NET8.PluginUtility.dll')
  $destination = Join-Path $PSScriptRoot 'sdk'
  New-Item -ItemType Directory -Path $destination -Force | Out-Null
  foreach ($name in $files) {
    $matches = @($zip.Entries | Where-Object { $_.Name -eq $name })
    if ($matches.Count -ne 1) { throw "Expected exactly one '$name' in official SDK ZIP; found $($matches.Count)." }
    $out = Join-Path $destination $name
    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($matches[0], $out, $true)
    Write-Host "Prepared SDK file: $out"
  }
  Write-Host 'SDK files are local-only. Do not commit SDK binaries to GitHub.' -ForegroundColor Green
} finally { $zip.Dispose() }
