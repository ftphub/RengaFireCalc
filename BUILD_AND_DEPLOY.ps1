# FireCalc v0.5.1 — run in Developer PowerShell for Visual Studio 2022/2026 (Windows).
# Without -Install: only test, compile and package. With -Install: backups then deployment.
param(
    [switch]$Install,
    [string]$RengaPluginsPath = 'C:\Program Files\Renga Professional\Plugins\FireCalc'
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$LogFile = Join-Path $PSScriptRoot 'FireCalc_build_log.txt'
Start-Transcript -Path $LogFile -Force | Out-Null
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw '.NET 8 SDK not found. Install the .NET 8 Desktop development workload in Visual Studio.'
    }
    if (-not (Get-Command msbuild -ErrorAction SilentlyContinue)) {
        throw 'MSBuild not found. Start Developer PowerShell for Visual Studio 2022/2026.'
    }
    & (Join-Path $PSScriptRoot 'CHECK_PREREQUISITES.ps1')
    Write-Host '1/3 Run independent Core tests (Renga is not needed)...' -ForegroundColor Cyan
    dotnet run --project '.\FireCalc.Tests\FireCalc.Tests.csproj' -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core tests FAILED — build cancelled.' }

    Write-Host '2/3 Rebuild Release/x64 and COM interop...' -ForegroundColor Cyan
    msbuild '.\FireCalc.sln' /m /t:Rebuild '/p:Configuration=Release;Platform=x64' /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'MSBuild FAILED — install cancelled.' }
    $BuiltDir = Join-Path $PSScriptRoot '_build\FireCalc'
    $MainDll = Join-Path $BuiltDir 'Renga.FireCalc.dll'
    foreach ($required in @('Renga.FireCalc.dll','FireCalc.Core.dll','Renga.NET8.PluginUtility.dll','FireCalc.rndesc')) {
        if (-not (Test-Path (Join-Path $BuiltDir $required))) { throw "Missing output: $required" }
    }
    $Archive = Join-Path $PSScriptRoot 'FireCalc_v0.5.1_COMPILED_Windows.zip'
    if (Test-Path $Archive) { Remove-Item $Archive -Force }
    Compress-Archive -Path (Join-Path $BuiltDir '*') -DestinationPath $Archive -CompressionLevel Optimal
    Write-Host "3/3 Windows build archive created: $Archive" -ForegroundColor Green

    if ($Install) {
        if (Get-Process -Name Renga -ErrorAction SilentlyContinue) {
            throw 'Renga is running. Exit Renga, then repeat with -Install.'
        }
        if (Test-Path $RengaPluginsPath) {
            $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
            $backupRoot = Join-Path $env:USERPROFILE 'Documents\FireCalcBackups'
            New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
            $backup = Join-Path $backupRoot "FireCalc_${stamp}"
            Copy-Item $RengaPluginsPath $backup -Recurse -Force
            Write-Host "Backup: $backup" -ForegroundColor Yellow
        }
        New-Item -ItemType Directory -Path $RengaPluginsPath -Force | Out-Null
        Copy-Item (Join-Path $BuiltDir '*') $RengaPluginsPath -Recurse -Force
        Write-Host "Installed to: $RengaPluginsPath" -ForegroundColor Green
    } else {
        Write-Host 'Not installed. To install after validation, run: .\BUILD_AND_DEPLOY.ps1 -Install'
    }
    Write-Host 'Build completed. Real Renga API/WPF behavior still requires manual testing.'
} catch {
    Write-Error $_
    exit 1
} finally {
    Stop-Transcript | Out-Null
}
