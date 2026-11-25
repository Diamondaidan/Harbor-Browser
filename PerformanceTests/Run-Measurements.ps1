[CmdletBinding()]
param([string]$BaselineExe,[switch]$Steady,[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$releaseRoot=Split-Path -Parent $PSScriptRoot
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if(!$msbuild){throw 'Visual Studio desktop build tools are required.'}
if(!$SkipBuild){
  & $msbuild (Join-Path $PSScriptRoot 'HarborPerformanceTests.csproj') /restore /p:Configuration=Release /v:minimal
  if($LASTEXITCODE -ne 0){throw 'Performance host build failed.'}
}
$output=Join-Path $releaseRoot ('T\P'+(Get-Date -Format 'MMddHHmmss'))
$candidate=Join-Path $output 'Runners\Candidate'
$candidateVersion=(Get-Item -LiteralPath (Join-Path $releaseRoot 'HarborBrowser\bin\Release\net48\HarborBrowser.exe')).VersionInfo.FileVersion.Trim()
New-Item -ItemType Directory -Path $candidate -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'bin\Release\net48') -Force | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $candidate -Recurse -Force}
Get-ChildItem -LiteralPath (Join-Path $releaseRoot 'HarborBrowser\bin\Release\net48') -Force | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $candidate -Recurse -Force}
$baseline=$null
if($BaselineExe){
  $baselineSource=(Resolve-Path -LiteralPath $BaselineExe).Path
  if((Get-Item -LiteralPath $baselineSource).VersionInfo.FileVersion.Trim() -ne '0.4.0.0'){throw 'Baseline must be the tested 0.4.0 build.'}
  $baseline=Join-Path $output 'Runners\Baseline'
  New-Item -ItemType Directory -Path $baseline -Force | Out-Null
  Get-ChildItem -LiteralPath $candidate -Force | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $baseline -Recurse -Force}
  Get-ChildItem -LiteralPath (Split-Path -Parent $baselineSource) -Force | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $baseline -Recurse -Force}
}
$fixtures=Join-Path $releaseRoot 'IntegrationTests\Fixtures'
function Run-Case([string]$runner,[string]$version,[string]$scene,[int]$tabs,[int]$trial){
  $caseOutput=Join-Path $output ($version+'-'+$scene+'-'+$tabs+'-trial'+$trial)
  $measurementMode=if($Steady){'--steady'}else{'--short'}
  & (Join-Path $runner 'HarborIntegrationTests.exe') $caseOutput $fixtures $tabs $trial $version $scene $measurementMode
  if($LASTEXITCODE -ne 0){throw "Benchmark failed: $version $scene $tabs trial $trial"}
}
if($Steady){foreach($trial in 1,2){Run-Case $candidate $candidateVersion 'idle' 20 $trial}}
else {
foreach($trial in 1,2){foreach($tabs in 5,10,20){Run-Case $candidate $candidateVersion 'idle' $tabs $trial}}
foreach($trial in 1,2){
  if($baseline){Run-Case $baseline '0.4.0.0' 'automatic' 10 $trial}
  Run-Case $candidate $candidateVersion 'automatic' 10 $trial
}
Run-Case $candidate $candidateVersion 'video' 10 1
Run-Case $candidate $candidateVersion 'download' 10 1
}
$measurements=Get-ChildItem -LiteralPath $output -Directory | Where-Object Name -ne 'Runners' | ForEach-Object {Get-Content -LiteralPath (Join-Path $_.FullName 'measurements.json') -Raw | ConvertFrom-Json}
$measurements | Select-Object Version,Scene,Tabs,Trial,MeasurementMode,StartupReadyMilliseconds,Runtime,LogicalProcessors | Export-Csv -LiteralPath (Join-Path $output 'startup.csv') -NoTypeInformation
$allSamples=foreach($measurement in $measurements){foreach($sample in $measurement.Samples){[PSCustomObject]@{Version=$measurement.Version;Scene=$measurement.Scene;Tabs=$measurement.Tabs;Trial=$measurement.Trial;Phase=$sample.Phase;Sleeping=$sample.Sleeping;Unloaded=$sample.Unloaded;AppWorkingMB=$sample.AppWorkingMB;AppPrivateMB=$sample.AppPrivateMB;WebWorkingMB=$sample.WebWorkingMB;WebPrivateMB=$sample.WebPrivateMB;CpuPercent=$sample.CpuPercent;ProtectedTabAwake=$sample.ProtectedTabAwake;DownloadBytes=$sample.DownloadBytes;LogicalProcessors=$measurement.LogicalProcessors;Runtime=$measurement.Runtime}}}
$allSamples | Export-Csv -LiteralPath (Join-Path $output 'samples.csv') -NoTypeInformation
$summary=$allSamples | Group-Object Version,Scene,Tabs,Phase | ForEach-Object {$sample=$_.Group[0];[PSCustomObject]@{Version=$sample.Version;Scene=$sample.Scene;Tabs=$sample.Tabs;Phase=$sample.Phase;Samples=$_.Count;Sleeping=($_.Group|Measure-Object Sleeping -Average).Average;Unloaded=($_.Group|Measure-Object Unloaded -Average).Average;CpuPercent=($_.Group|Measure-Object CpuPercent -Average).Average;AppWorkingMB=($_.Group|Measure-Object AppWorkingMB -Average).Average;AppPrivateMB=($_.Group|Measure-Object AppPrivateMB -Average).Average;WebWorkingMB=($_.Group|Measure-Object WebWorkingMB -Average).Average;WebPrivateMB=($_.Group|Measure-Object WebPrivateMB -Average).Average}}
$summary | Export-Csv -LiteralPath (Join-Path $output 'summary.csv') -NoTypeInformation
Write-Host "Results: $output"
$summary | Format-Table Version,Scene,Tabs,Phase,Sleeping,Unloaded,CpuPercent,WebWorkingMB,WebPrivateMB
