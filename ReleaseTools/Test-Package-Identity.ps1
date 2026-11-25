[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Installer)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
. (Join-Path $PSScriptRoot 'Build-Identity.ps1')
$identity=Get-HarborBuildIdentity $root -VerifyCompiled
if($identity.Channel -eq 'release'){throw 'These candidate-rejection fixtures require an RC checkout.'}
$owned=Join-Path $root ('T\identity071-'+[guid]::NewGuid().ToString('N').Substring(0,8))
$compiled=Join-Path $owned 'HarborBrowser\bin\Release\net48'
New-Item -ItemType Directory -Path $compiled -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $owned '.owned-identity-fixture'),'Candidate packaging guard checks only.')
$checks=0
function Check([string]$name,[bool]$ok){if(!$ok){throw $name};$script:checks++;Write-Output ('PASS '+$name)}
function Reject([string]$name,[scriptblock]$action,[string]$message){
 $caught=$false
 try{& $action | Out-Null}catch{if($_.Exception.Message -notlike ('*'+$message+'*')){throw};$caught=$true}
 Check $name $caught
}
foreach($name in @('HarborBrowser.exe','HarborUpdateLauncher.exe')){Copy-Item -LiteralPath (Join-Path $root ('HarborBrowser\bin\Release\net48\'+$name)) -Destination $compiled}
[IO.File]::WriteAllText((Join-Path $owned 'VERSION.txt'),$identity.Version)
[IO.File]::WriteAllText((Join-Path $owned 'RELEASE-CHANNEL.txt'),$identity.Channel)
Check 'matching source and both compiled build identities accepted' ((Get-HarborBuildIdentity $owned -VerifyCompiled).InformationalVersion -eq $identity.InformationalVersion)
[IO.File]::WriteAllText((Join-Path $owned 'RELEASE-CHANNEL.txt'),'release')
Reject 'stable source channel cannot package candidate executables' {Get-HarborBuildIdentity $owned -VerifyCompiled} 'does not match'
[IO.File]::WriteAllText((Join-Path $owned 'RELEASE-CHANNEL.txt'),'rc0')
Reject 'invalid source candidate number refused' {Get-HarborBuildIdentity $owned -VerifyCompiled} 'Invalid Harbor source build identity'
[IO.File]::WriteAllText((Join-Path $owned 'RELEASE-CHANNEL.txt'),$identity.Channel)
Copy-Item -LiteralPath (Join-Path $root 'UpdateTests\bin\Release\net48\Fixtures\StableIdentity.exe') -Destination (Join-Path $compiled 'HarborUpdateLauncher.exe') -Force
Reject 'mismatched stable launcher cannot accompany candidate browser' {Get-HarborBuildIdentity $owned -VerifyCompiled} 'does not match'
Reject 'portable builder refuses a false release suffix' {& (Join-Path $root 'Build-Release.ps1') -SkipBuild -BuildSuffix '-release'} 'does not match'
Reject 'installer builder refuses a false release candidate' {& (Join-Path $root 'Build-Installer.ps1') -SkipBuild -Candidate release} 'does not match'
$setup=Get-Item -LiteralPath $Installer
Check 'installer resource keeps candidate ProductVersion' ($setup.VersionInfo.ProductVersion.Trim() -eq $identity.InformationalVersion)
Check 'installer name keeps candidate channel' ($setup.Name -eq ('HarborBrowserSetup-'+$identity.Version+'-'+$identity.Channel+'.exe'))
$renamed=Join-Path $owned ('HarborBrowserSetup-'+$identity.Version+'-release.exe')
Copy-Item -LiteralPath $setup.FullName -Destination $renamed
foreach($path in @($setup.FullName,$renamed)){
 Reject ('stable feed refuses candidate '+[IO.Path]::GetFileName($path)) {& (Join-Path $PSScriptRoot 'New-Update-Record.ps1') -Installer $path -PrivateKeyPath (Join-Path $owned 'intentionally-missing-key.dpapi') -Sequence 1 -OutputPath (Join-Path $owned 'must-not-exist.json') -AllowUnsigned} 'Only a full-release installer'
}
Check 'candidate rejection occurs before key access or output creation' (!(Test-Path -LiteralPath (Join-Path $owned 'must-not-exist.json')))
[pscustomobject]@{Passed=$true;Checks=$checks;BuildIdentity=$identity.InformationalVersion;AppSHA256=(Get-FileHash -LiteralPath (Join-Path $root 'HarborBrowser\bin\Release\net48\HarborBrowser.exe')).Hash;InstallerSHA256=(Get-FileHash -LiteralPath $setup.FullName).Hash;PrivateKeyAccessed=$false;Evidence=$owned.Substring($root.Length+1).Replace('\','/');RecordedUtc=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'PACKAGE-IDENTITY-VERIFICATION.json') -Encoding utf8
Write-Output ('ALL PASSED: '+$checks+' package identity and candidate rejection checks.')
