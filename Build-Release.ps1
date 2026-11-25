[CmdletBinding()]
param([switch]$SkipBuild,[ValidatePattern('^(-[A-Za-z0-9]+)?$')][string]$BuildSuffix='')
$ErrorActionPreference='Stop'
$releaseRoot=$PSScriptRoot
$version=(Get-Content -LiteralPath (Join-Path $releaseRoot 'VERSION.txt') -Raw).Trim()
if($version -ne '0.6.0'){throw 'Unexpected release version.'}
if(!$SkipBuild){
  $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
  $builder=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
  if(!$builder){throw 'Visual Studio desktop build tools are required.'}
  & $builder (Join-Path $releaseRoot 'HarborBrowser\HarborBrowser.csproj') /restore /p:Configuration=Release /v:minimal /nologo
  if($LASTEXITCODE -ne 0){throw 'Release build failed.'}
}
$compiled=Join-Path $releaseRoot 'HarborBrowser\bin\Release\net48'
$app=Join-Path $compiled 'HarborBrowser.exe'
if(!(Test-Path -LiteralPath $app) -or (Get-Item -LiteralPath $app).VersionInfo.FileVersion.Trim() -ne '0.6.0.0'){throw 'The expected trial executable is missing.'}
$verificationPath=Join-Path $releaseRoot 'RELEASE-VERIFICATION.json'
if(!(Test-Path -LiteralPath $verificationPath)){throw 'The release has no completed verification record. Do not package an unverified protection build.'}
$verification=Get-Content -LiteralPath $verificationPath -Raw | ConvertFrom-Json
if($verification.ReadyToPackage -ne $true -or $verification.AppSHA256 -ne (Get-FileHash -LiteralPath $app -Algorithm SHA256).Hash){throw 'Release packaging is blocked: protection checks have not passed for this executable. See RELEASE-VERIFICATION.md.'}
$provider=Join-Path $compiled 'Assets\BrowserVpn\Proton'
if(!(Test-Path -LiteralPath (Join-Path $provider 'manifest.json'))){throw 'Build the official Proton assets first; see ThirdParty/PROTON-BUILD.md.'}
$dist=Join-Path $releaseRoot 'dist'
$stage=Join-Path $dist ('Harbor-0.6.0'+$BuildSuffix)
if(![IO.Path]::GetFullPath($stage).StartsWith([IO.Path]::GetFullPath($releaseRoot)+'\dist\',[StringComparison]::OrdinalIgnoreCase)){throw 'Portable stage escaped the release.'}
if(Test-Path -LiteralPath $stage){throw 'Portable stage already exists. Preserve it while testing; use a fresh stage for a later build.'}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
& robocopy $compiled $stage /E /XD TorRuntime Router Control TrialData /XF *.pdb *.xml /NFL /NDL /NJH /NJS /NP
if($LASTEXITCODE -ge 8){throw 'Portable staging failed.'}
Copy-Item -LiteralPath (Join-Path $releaseRoot 'RELEASE-NOTES.md') -Destination (Join-Path $stage 'READ-ME-FIRST.md')
Copy-Item -LiteralPath (Join-Path $releaseRoot 'RELEASE-VERIFICATION.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $releaseRoot 'RELEASE-VERIFICATION.json') -Destination $stage
foreach($report in @('SETUP-README.md','LIVE-VERIFICATION.md','UPDATING.md','INSTALLER-VERIFICATION.md')){
  if(Test-Path -LiteralPath (Join-Path $releaseRoot $report)){Copy-Item -LiteralPath (Join-Path $releaseRoot $report) -Destination $stage}
}
$licensing=Join-Path $stage 'Licensing'
New-Item -ItemType Directory -Path $licensing -Force | Out-Null
foreach($name in @('ProtonVPN-source.zip','PROTON-REVISION.txt','PROTON-BUILD.md','PROTON-PROVENANCE.json','PROTON-PACKAGE-HASHES.json')){
  Copy-Item -LiteralPath (Join-Path $releaseRoot ('ThirdParty\'+$name)) -Destination $licensing
}
Copy-Item -LiteralPath (Join-Path $releaseRoot 'ThirdParty\ProtonBuild\pnpm-lock.yaml') -Destination $licensing
Copy-Item -LiteralPath (Join-Path $releaseRoot 'ThirdParty\ProtonBuild\Build-Proton.ps1') -Destination $licensing
Copy-Item -LiteralPath (Join-Path $releaseRoot 'ThirdParty\WebView2') -Destination $licensing -Recurse
Copy-Item -LiteralPath (Join-Path $releaseRoot 'THIRD-PARTY-NOTICES.md') -Destination $licensing
Copy-Item -LiteralPath (Join-Path $releaseRoot 'ThirdParty\ProtonDependencyNotices') -Destination $licensing -Recurse
Copy-Item -LiteralPath (Join-Path $releaseRoot 'ThirdParty\Proton-dependency-inputs.zip') -Destination $licensing
if(Test-Path -LiteralPath (Join-Path $stage 'TrialData')){throw 'A portable package must not contain a test or signed-in profile.'}
[IO.File]::WriteAllText((Join-Path $stage 'harbor-release-channel.txt'),"HarborStable")
$payloadFiles=@(Get-ChildItem -LiteralPath $stage -File -Recurse | ForEach-Object {
  [pscustomobject]@{Path=$_.FullName.Substring($stage.Length+1).Replace('\','/');Bytes=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
[pscustomobject]@{Channel='HarborStable';Version=$version;AppSHA256=$verification.AppSHA256;Files=$payloadFiles} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $stage 'package-manifest.json') -Encoding utf8
$zip=Join-Path $dist ('HarborBrowser-0.6.0'+$BuildSuffix+'-portable.zip')
if(Test-Path -LiteralPath $zip){throw 'Portable archive already exists; refusing to overwrite an unverified prior artifact.'}
Compress-Archive -LiteralPath $stage -DestinationPath $zip -CompressionLevel Optimal
Get-FileHash -LiteralPath $app,$zip -Algorithm SHA256
Write-Output ('Portable release: '+$zip)
Write-Output ('Extract into a writable folder. Run '+(Join-Path $stage 'HarborBrowser.exe'))

exit 0

