[CmdletBinding()]
param([string]$RollbackArchive='',[string]$SourceArchive='')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$artifact=Get-Content -LiteralPath (Join-Path $root 'INSTALLER-ARTIFACT.json') -Raw | ConvertFrom-Json
if((Get-FileHash -LiteralPath $artifact.Installer -Algorithm SHA256).Hash -ne $artifact.InstallerSHA256){throw 'Installer artifact changed.'}
$source=Join-Path $root 'TestTools\ConnectionCheck'
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$builder=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if(!$builder){throw 'Desktop build tools are required to prepare the connection-check helper.'}
& $builder (Join-Path $source 'HarborConnectedChecks.csproj') /restore /p:Configuration=Release /v:minimal /nologo *> (Join-Path $root 'connection-kit-build063.log')
if($LASTEXITCODE -ne 0){throw 'Connection-check helper build failed.'}
$kit=Join-Path $root ('dist\Harbor-0.6.3-'+$artifact.Candidate+'-Spare-PC-Kit')
if(Test-Path -LiteralPath $kit){throw 'Preserve the existing kit; use a new candidate.'}
New-Item -ItemType Directory -Path $kit,(Join-Path $kit 'ConnectionCheck') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'Spare-PC-Tools') -File | Copy-Item -Destination $kit
Copy-Item -LiteralPath $artifact.Installer -Destination $kit
Copy-Item -LiteralPath (Join-Path $root ('dist\HarborBrowser-0.6.3-'+$artifact.Candidate+'-portable.zip')) -Destination $kit
Copy-Item -LiteralPath (Join-Path $root 'SETUP-README.md'),(Join-Path $root 'RELEASE-VERIFICATION.md'),(Join-Path $root 'INSTALLER-VERIFICATION.md'),(Join-Path $root 'LIVE-VERIFICATION.md'),(Join-Path $root 'INSTALLER-ARTIFACT.json') -Destination $kit
foreach($report in @('RELIABILITY-0.6.3-VERIFICATION.md','LOCAL-PACKAGE-VERIFICATION.md','PACKAGED-PAYLOAD-VERIFICATION.json')){if(Test-Path -LiteralPath (Join-Path $root $report)){Copy-Item -LiteralPath (Join-Path $root $report) -Destination $kit}}
if($SourceArchive){
 $sourceZip=[IO.Path]::GetFullPath($SourceArchive)
 if(!(Test-Path -LiteralPath $sourceZip -PathType Leaf)){throw 'Source archive was not found.'}
 Copy-Item -LiteralPath $sourceZip -Destination $kit
}
$localRecord=Join-Path $root 'dist\Local-Release-063\harbor-update.json'
if(Test-Path -LiteralPath $localRecord){Copy-Item -LiteralPath $localRecord -Destination $kit}
$reference=Join-Path $source 'bin\Release\net48'
foreach($name in @('HarborConnectedChecks.exe','HarborConnectedChecks.exe.config','index.html','app.js','worker.js','sw.js')){
 Copy-Item -LiteralPath (Join-Path $reference $name) -Destination (Join-Path $kit 'ConnectionCheck')
}
Copy-Item -LiteralPath (Join-Path $source 'Program.cs'),(Join-Path $source 'HarborConnectedChecks.csproj') -Destination (Join-Path $kit 'ConnectionCheck')
if($RollbackArchive){
 $rollback=[IO.Path]::GetFullPath($RollbackArchive)
 if(!(Test-Path -LiteralPath $rollback -PathType Leaf)){throw 'Rollback archive not found.'}
 Copy-Item -LiteralPath $rollback -Destination (Join-Path $kit ([IO.Path]::GetFileName($rollback)))
}
$files=@(Get-ChildItem -LiteralPath $kit -File -Recurse | ForEach-Object {[pscustomobject]@{Path=$_.FullName.Substring($kit.Length+1).Replace('\','/');SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;Bytes=$_.Length}})
[pscustomobject]@{Version='0.6.3-'+$artifact.Candidate;Channel='HarborStable';Files=$files;AutomaticInstallation=$false;AccountAccess=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $kit 'kit-manifest.json') -Encoding utf8
& (Join-Path $kit 'Check-This-PC.ps1')
if($LASTEXITCODE -ne 0){throw 'Read-only kit preflight failed.'}
$results=[IO.Path]::GetFullPath((Join-Path $kit 'Results'))
$saved=[IO.Path]::GetFullPath((Join-Path $root ('Evidence\057\kit-preflight-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))))
if(!$results.StartsWith([IO.Path]::GetFullPath($root)+'\',[StringComparison]::OrdinalIgnoreCase)-or !$saved.StartsWith([IO.Path]::GetFullPath($root)+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Preflight report paths escaped the release.'}
New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
Move-Item -LiteralPath $results -Destination $saved
$zip=$kit+'.zip'
Compress-Archive -LiteralPath $kit -DestinationPath $zip -CompressionLevel Optimal
[pscustomobject]@{Kit=$kit;Zip=$zip;SHA256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash;Bytes=(Get-Item $zip).Length} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $root 'SPARE-PC-KIT.json')

