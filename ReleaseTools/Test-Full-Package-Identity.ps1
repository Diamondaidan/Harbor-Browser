[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Installer)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
. (Join-Path $PSScriptRoot 'Build-Identity.ps1')
$identity=Get-HarborBuildIdentity $root -VerifyCompiled
if($identity.Channel -ne 'release'){throw 'Use a full-release checkout for these checks.'}
$owned=Join-Path $root ('T\full-identity-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $owned | Out-Null
[IO.File]::WriteAllText((Join-Path $owned '.owned-identity-fixture'),'Full-package metadata checks only; no installer or private key execution.')
$checks=0
function Check([string]$name,[bool]$ok){if(!$ok){throw $name};$script:checks++;Write-Output ('PASS '+$name)}
function Reject([string]$name,[scriptblock]$action,[string]$message){
 $caught=$false
 try{& $action | Out-Null}catch{if($_.Exception.Message -notlike ('*'+$message+'*')){throw};$caught=$true}
 Check $name $caught
}
Check 'source app and helper carry full-release identity' ($identity.InformationalVersion -eq $identity.Version)
$setup=Get-Item -LiteralPath $Installer
Check 'full installer has canonical release filename' ($setup.Name -eq ('HarborBrowserSetup-'+$identity.Version+'-release.exe'))
Check 'full installer has stable ProductVersion and numeric FileVersion' ($setup.VersionInfo.ProductVersion.Trim() -eq $identity.Version -and $setup.VersionInfo.FileVersion.Trim() -eq ($identity.Version+'.0'))
Check 'full installer identifies Harbor Browser' ($setup.VersionInfo.ProductName.Trim() -eq 'Harbor Browser')
$manifest=Get-Content -LiteralPath (Join-Path $root ('dist\Harbor-'+$identity.Version+'-release\package-manifest.json')) -Raw | ConvertFrom-Json
Check 'full payload has matching release channel and identity' ($manifest.Build -eq 'release' -and $manifest.BuildIdentity -eq $identity.InformationalVersion)
$record=Get-Content -LiteralPath (Join-Path $root 'RELEASE-VERIFICATION.json') -Raw | ConvertFrom-Json
Check 'full payload matches the verified executable' ($manifest.AppSHA256 -eq $record.AppSHA256 -and $record.BuildIdentity -eq $identity.InformationalVersion)
Reject 'portable builder refuses a false RC suffix' {& (Join-Path $root 'Build-Release.ps1') -SkipBuild -BuildSuffix '-rc1'} 'does not match'
Reject 'installer builder refuses a false RC label' {& (Join-Path $root 'Build-Installer.ps1') -SkipBuild -Candidate rc1} 'does not match'
$falseName=Join-Path $owned ('HarborBrowserSetup-'+$identity.Version+'-rc1.exe')
Copy-Item -LiteralPath $setup.FullName -Destination $falseName
$rc=Get-Item -LiteralPath (Join-Path $root 'UpdateTests\bin\Release\net48\Fixtures\CandidateIdentity.exe')
if($rc.VersionInfo.FileVersion.Trim() -notmatch '^(\d+\.\d+\.\d+)\.0$'){throw 'Invalid RC identity fixture.'}
$rcVersion=$Matches[1]
$falseMetadata=Join-Path $owned ('HarborBrowserSetup-'+$rcVersion+'-release.exe')
Copy-Item -LiteralPath $rc.FullName -Destination $falseMetadata
foreach($path in @($falseName,$falseMetadata)){
 Reject ('stable-feed signing refuses contradictory '+[IO.Path]::GetFileName($path)) {& (Join-Path $PSScriptRoot 'New-Update-Record.ps1') -Installer $path -PrivateKeyPath (Join-Path $owned 'intentionally-missing-key.dpapi') -Sequence 1 -OutputPath (Join-Path $owned 'must-not-exist.json') -AllowUnsigned} 'Only a full-release installer'
}
Check 'rejection occurs before private-key access or signed output' (!(Test-Path -LiteralPath (Join-Path $owned 'must-not-exist.json')))
[pscustomobject]@{Passed=$true;Checks=$checks;BuildIdentity=$identity.InformationalVersion;AppSHA256=$record.AppSHA256;InstallerSHA256=(Get-FileHash -LiteralPath $setup.FullName).Hash;PrivateKeyAccessed=$false;InstallerExecuted=$false;Evidence=$owned.Substring($root.Length+1).Replace('\','/');RecordedUtc=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'PACKAGE-IDENTITY-VERIFICATION.json') -Encoding utf8
Write-Output ('ALL PASSED: '+$checks+' full-package identity checks.')
