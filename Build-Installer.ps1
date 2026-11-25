[CmdletBinding()]
param([ValidatePattern('^(release|rc[0-9]+)$')][string]$Candidate='release',[switch]$StageOnly,[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION.txt') -Raw).Trim()
if($version -ne '0.5.7'){throw 'Unexpected beta version.'}
if(!$SkipBuild){
 $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
 $builder=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
 if(!$builder){throw 'Desktop MSBuild is required.'}
 & $builder (Join-Path $root 'HarborBrowser\HarborBrowser.csproj') /restore /p:Configuration=Release /v:minimal /nologo
 if($LASTEXITCODE -ne 0){throw 'Beta build failed.'}
}
$record=Get-Content -LiteralPath (Join-Path $root 'RELEASE-VERIFICATION.json') -Raw | ConvertFrom-Json
$app=Join-Path $root 'HarborBrowser\bin\Release\net48\HarborBrowser.exe'
$appHash=(Get-FileHash -LiteralPath $app -Algorithm SHA256).Hash
if(!$record.ReadyToPackage -or $record.AppSHA256 -ne $appHash){throw 'Run and record the complete current beta tests before packaging.'}
$stage=Join-Path $root ('dist\Harbor-'+$version+'-'+$Candidate)
if(!(Test-Path -LiteralPath $stage)){& (Join-Path $root 'Build-Release.ps1') -SkipBuild -BuildSuffix ('-'+$Candidate);if($LASTEXITCODE -ne 0){throw 'Payload staging failed.'}}
$manifest=Get-Content -LiteralPath (Join-Path $stage 'package-manifest.json') -Raw | ConvertFrom-Json
if($manifest.Channel -ne 'HarborStable' -or $manifest.AppSHA256 -ne $appHash){throw 'Existing candidate payload does not match. Use a new candidate name.'}
foreach($entry in $manifest.Files){
 $file=Join-Path $stage $entry.Path
 if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.SHA256){throw ('Staged payload changed: '+$entry.Path)}
}
$redist=Join-Path $root 'Installer\redist'
$expected=Get-Content -LiteralPath (Join-Path $root 'Installer\redist-manifest.json') -Raw | ConvertFrom-Json
foreach($entry in $expected){
 $file=Join-Path $redist $entry.Name
 if(!(Test-Path -LiteralPath $file)){throw 'The signed offline prerequisite packages are missing. See Installer/Get-Prerequisites.ps1.'}
 if((Get-Item -LiteralPath $file).Length -ne $entry.Bytes -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.SHA256){throw ('Prerequisite hash/size changed: '+$entry.Name)}
 $signature=Get-AuthenticodeSignature -LiteralPath $file
 if($signature.Status -ne 'Valid' -or !$signature.SignerCertificate -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation'){throw 'A Microsoft prerequisite signature could not be verified.'}
}
if($StageOnly){Write-Output ('Verified beta payload and offline prerequisites: '+$stage);return}
$fixtureRecord=Join-Path $root 'INSTALLER-VERIFICATION.json'
if(!(Test-Path -LiteralPath $fixtureRecord)){throw 'Run the native installer/prerequisite fixtures first.'}
$fixtures=Get-Content -LiteralPath $fixtureRecord -Raw | ConvertFrom-Json
$templateHash=(Get-FileHash -LiteralPath (Join-Path $root 'Installer\HarborBrowser.iss')).Hash
$registryHash=(Get-FileHash -LiteralPath (Join-Path $root 'Installer\BrowserRegistry.iss')).Hash
if(!$fixtures.Passed -or $fixtures.TemplateSHA256 -ne $templateHash -or $fixtures.RegistrySHA256 -ne $registryHash -or $fixtures.AppSHA256 -ne $appHash){throw 'Installer fixtures are stale or failed.'}
$compiler=Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'
if(!(Test-Path -LiteralPath $compiler)){throw 'Inno Setup 7 is required.'}
$output=Join-Path $root ('dist\HarborBrowserSetup-'+$version+'-'+$Candidate+'.exe')
if(Test-Path -LiteralPath $output){throw 'Candidate installer already exists; preserve it and use a fresh candidate.'}
& $compiler /Qp ('/DAppVersion='+$version) ('/DCandidate='+$Candidate) ('/DPayloadDir='+$stage) (Join-Path $root 'Installer\HarborBrowser.iss')
if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $output)){throw 'Beta installer compilation failed.'}
[pscustomobject]@{Version=$version;Candidate=$Candidate;AppSHA256=$appHash;InstallerSHA256=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash;InstallerBytes=(Get-Item -LiteralPath $output).Length;Installer=$output;OfflinePrerequisites=$expected;NativeFixturesPassed=$true;LiveProtonValidation='Pending';CleanWindowsInstall='Pending';Profile='HarborBrowser'} | ConvertTo-Json -Depth 5 | Tee-Object -FilePath (Join-Path $root 'INSTALLER-ARTIFACT.json')

