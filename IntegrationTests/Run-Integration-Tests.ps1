[CmdletBinding()]
param([switch]$SuggestionClicks)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if(!$msbuild){throw 'Visual Studio desktop build tools are required.'}
& $msbuild (Join-Path $PSScriptRoot 'HarborIntegrationTests.csproj') /restore /p:Configuration=Release /v:minimal
if($LASTEXITCODE -ne 0){throw 'Integration build failed.'}
$testBin=Join-Path $PSScriptRoot 'bin\Release\net48'
Copy-Item -LiteralPath (Join-Path $root 'HarborBrowser\bin\Release\net48\Assets') -Destination $testBin -Recurse -Force
$output=Join-Path $PSScriptRoot ('TestResults\'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$testArgs=@($output,(Join-Path $PSScriptRoot 'Fixtures'))
if($SuggestionClicks){$testArgs+='--suggestion-clicks'}
& (Join-Path $testBin 'HarborIntegrationTests.exe') @testArgs
if($LASTEXITCODE -ne 0){throw 'Integration tests failed.'}
Write-Host "Results: $output"
