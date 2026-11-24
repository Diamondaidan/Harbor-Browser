[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$BaselineExe,[int]$Trials=3)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$baselineSource=(Get-Item -LiteralPath $BaselineExe).FullName
if((Get-Item -LiteralPath $baselineSource).VersionInfo.FileVersion.Trim() -ne '0.6.3.0'){throw 'Use the frozen 0.6.3 baseline.'}
if((Get-FileHash -LiteralPath $baselineSource).Hash -ne '103840485A9B49B361B632A22BDD8CF3747AFAB4A77D05DEABE7E2B528D097C1'){throw 'Unexpected baseline bytes.'}
$output=Join-Path $root ('T\S'+(Get-Date -Format 'MMddHHmmss'))
$records=@()
foreach($label in @('Baseline','Candidate')){
 $runner=Join-Path $output ('Runners\'+$label)
 [IO.Directory]::CreateDirectory($runner)|Out-Null
 Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'bin\Release\net48') -Force|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $runner -Recurse -Force}
 $source=if($label -eq 'Baseline'){Split-Path $baselineSource}else{Join-Path $root 'HarborBrowser\bin\Release\net48'}
 Get-ChildItem -LiteralPath $source -Force|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $runner -Recurse -Force}
}
foreach($trial in 1..$Trials){foreach($label in @('Baseline','Candidate')){
 $runner=Join-Path $output ('Runners\'+$label)
 $version=(Get-Item -LiteralPath (Join-Path $runner 'HarborBrowser.exe')).VersionInfo.FileVersion.Trim()
 $caseOutput=Join-Path $output ($label+'-'+$trial)
 & (Join-Path $runner 'HarborIntegrationTests.exe') $caseOutput (Join-Path $root 'IntegrationTests\Fixtures') 20 $trial $version restore --short
 if($LASTEXITCODE -ne 0){throw 'Restored-session startup measurement failed.'}
 $records+=Get-Content -LiteralPath (Join-Path $caseOutput 'measurements.json') -Raw|ConvertFrom-Json
}}
$records|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'restore-startup.json') -Encoding UTF8
$records|Export-Csv -LiteralPath (Join-Path $output 'restore-startup.csv') -NoTypeInformation
Write-Output ('Startup comparison results: '+$output)