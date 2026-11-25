[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = if (Test-Path $vswhere) {
    & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
} else { (Get-Command msbuild.exe -ErrorAction SilentlyContinue).Source }
if (!$msbuild) { throw 'Install Visual Studio 2022 Build Tools with .NET desktop tools and the .NET Framework 4.8 Developer Pack.' }
& $msbuild (Join-Path $PSScriptRoot 'HarborRegressionTests.csproj') /restore /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
$output = Join-Path $PSScriptRoot ('TestResults\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
& (Join-Path $PSScriptRoot 'bin\Release\net48\HarborRegressionTests.exe') $output (Join-Path $PSScriptRoot 'Fixtures\HarborBrowser-0.3.1-build-fix.zip') (Join-Path $root 'HarborBrowser\bin\Release\net48\HarborBrowser.exe') (Join-Path $root 'HarborBrowser\bin\Release\net48\Microsoft.Web.WebView2.Core.dll') (Join-Path $root 'HarborBrowser\MainWindow.xaml')
if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
Write-Host "Results and offline layout previews: $output"
