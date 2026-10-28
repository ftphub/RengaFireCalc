# Run in "Developer PowerShell for Visual Studio 2022" with .NET 8 SDK and x64 build tools.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
Write-Host "=== Core tests (no Renga required) ===" -ForegroundColor Cyan
dotnet run --project .\FireCalc.Tests\FireCalc.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "Core tests failed" }
Write-Host "=== Renga FireCalc plugin ===" -ForegroundColor Cyan
msbuild .\FireCalc.sln /m /t:Build /p:Configuration=Release /p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed" }
Write-Host "Artifacts: .\_build\FireCalc\" -ForegroundColor Green
Write-Host "Deploy all DLLs and FireCalc.rndesc from that folder to [Renga installation]\Plugins\FireCalc\"
