[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'Visual Studio 2022 Build Tools with .NET desktop tools are required.' }
& $msbuild (Join-Path $PSScriptRoot 'HarborHomeSmoke.csproj') /restore /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Browser test build failed.' }
$output = Join-Path $PSScriptRoot ('TestResults\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
& (Join-Path $PSScriptRoot 'bin\Release\net48\HarborHomeSmoke.exe') (Join-Path $root 'dist\payload\Assets\Home') $output
if ($LASTEXITCODE -ne 0) { throw 'Browser tests failed.' }
Write-Host "Results and actual WebView2 captures: $output"
