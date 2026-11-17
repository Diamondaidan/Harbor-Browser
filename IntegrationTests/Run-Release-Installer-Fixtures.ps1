[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot))
$owned=Join-Path $root ('T\installer057-'+[guid]::NewGuid().ToString('N').Substring(0,10))
$install=Join-Path $owned 'App'
$profile=Join-Path $owned 'Profile'
$payload=Join-Path $owned 'Payload'
$redist=Join-Path $owned 'StubRedist'
$nonce=[guid]::NewGuid().ToString('N')
$regPrefix='Software\Harbor057Fixtures\'+$nonce
$appId='Harbor057Fixture-'+$nonce
$compiler=Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'
$checks=0
foreach($path in @($owned,$install,$profile,$payload,$redist)){
 if(![IO.Path]::GetFullPath($path).StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture escaped workspace.'}
}
New-Item -ItemType Directory -Path $owned,$profile,$payload,$redist,(Join-Path $payload 'Branding') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $owned '.owned-installer-fixture') -Value $nonce -Encoding ascii
Copy-Item -LiteralPath (Join-Path $root 'HarborBrowser\bin\Release\net48\HarborBrowser.exe') -Destination $payload
Copy-Item -LiteralPath (Join-Path $root 'HarborBrowser\bin\Release\net48\HarborUpdateLauncher.exe') -Destination $payload
Copy-Item -LiteralPath (Join-Path $root 'HarborBrowser\Branding\HarborBeacon.ico') -Destination (Join-Path $payload 'Branding')
Set-Content -LiteralPath (Join-Path $payload 'harbor-release-channel.txt') -Value 'HarborStable' -Encoding ascii
$stub=@'
using System;
using System.IO;
using System.Reflection;
class Stub {
 static int Main(string[] args) {
  string name=Path.GetFileName(Assembly.GetExecutingAssembly().Location);
  File.AppendAllText(Environment.GetEnvironmentVariable("HARBOR057_TRACE"),name+"|"+string.Join(" ",args)+Environment.NewLine);
  return int.Parse(Environment.GetEnvironmentVariable(name.StartsWith("ndp")?"HARBOR057_NET_CODE":"HARBOR057_WEB_CODE"));
 }
}
'@
$source=Join-Path $owned 'Stub.cs'
[IO.File]::WriteAllText($source,$stub)
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /target:exe ('/out:'+(Join-Path $redist 'ndp48-x86-x64-allos-enu.exe')) $source
if($LASTEXITCODE -ne 0){throw 'Stub compile failed.'}
Copy-Item -LiteralPath (Join-Path $redist 'ndp48-x86-x64-allos-enu.exe') -Destination (Join-Path $redist 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe')
function Check([string]$name,[bool]$ok){if(!$ok){throw $name};$script:checks++;Write-Output ('PASS '+$name)}
function Run-Setup([string]$exe,[string]$dest,[string]$label){
 $args=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS','/RESTARTEXITCODE=3010',('/DIR="'+$dest+'"'),('/LOG="'+(Join-Path $owned ($label+'.log'))+'"'))
 $process=Start-Process -FilePath $exe -ArgumentList $args -WindowStyle Hidden -Wait -PassThru
 $result=$process.ExitCode;$process.Dispose();return $result
}
function Choices {
 $values=@()
 foreach($scheme in @('http','https')){
  $key=Get-ItemProperty -LiteralPath ('HKCU:\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\'+$scheme+'\UserChoice') -ErrorAction SilentlyContinue
  $values+=@{Id=$key.ProgId;Hash=$key.Hash}
 }
 $values | ConvertTo-Json -Compress
}
function Compile([string]$candidate,[int]$revision){
 $args=@('/Qp','/DFixtureBuild=1','/DBrowserRegistryRoot=HKCU',('/DFixtureInstallDir='+$install),('/DFixtureProfileDir='+$profile.Replace("'","''")),('/DPayloadDir='+$payload),('/DRedistDir='+$redist),('/DOutputFolder='+$owned),('/DCandidate='+$candidate),('/DBuildRevision='+$revision),('/DReleaseAppId='+$appId),('/DAppName=Harbor057Fixture'),('/DRegistryPrefix='+$regPrefix),(Join-Path $root 'Installer\HarborBrowser.iss'))
 & $compiler @args *> (Join-Path $owned ($candidate+'-compile.txt'))
 if($LASTEXITCODE -ne 0){Get-Content (Join-Path $owned ($candidate+'-compile.txt')) -Tail 12;throw 'Fixture compile failed.'}
 Join-Path $owned ('HarborBrowserSetup-0.6.0-'+$candidate+'.exe')
}
$vars=@('HARBOR057_TRACE','HARBOR057_NET_CODE','HARBOR057_WEB_CODE','HARBOR057_PRESENT','HARBOR057_NET_AFTER','HARBOR057_WEB_AFTER')
$saved=@{};foreach($key in $vars){$saved[$key]=[Environment]::GetEnvironmentVariable($key)}
$choicesBefore=Choices
$uninstall=Join-Path $install 'unins000.exe'
$passed=$false
try{
 $first=Compile 'fixture1' 0;$second=Compile 'fixture2' 1
 $env:HARBOR057_PRESENT='1';$env:HARBOR057_NET_AFTER='1';$env:HARBOR057_WEB_AFTER='1';$env:HARBOR057_NET_CODE='0';$env:HARBOR057_WEB_CODE='0'
 $env:HARBOR057_TRACE=Join-Path $owned 'lifecycle-trace.txt'
 Check 'native isolated stable fixture installs' ((Run-Setup $first $install 'install') -eq 0)
 $appHash=(Get-FileHash -LiteralPath (Join-Path $payload 'HarborBrowser.exe')).Hash
 Check 'installed executable matches the compiled stable app' ((Get-FileHash -LiteralPath (Join-Path $install 'HarborBrowser.exe')).Hash -eq $appHash)
 Check 'installed update launcher matches the compiled helper' ((Get-FileHash -LiteralPath (Join-Path $install 'HarborUpdateLauncher.exe')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $payload 'HarborUpdateLauncher.exe')).Hash)
 $cap='HKCU:\'+$regPrefix+'\Clients\StartMenuInternet\HarborBrowser\Capabilities'
 Check 'stable fixture advertises its own HTTP and HTML handlers' (((Get-ItemProperty ($cap+'\URLAssociations')).https -eq 'HarborBrowser.URL') -and ((Get-ItemProperty ($cap+'\FileAssociations')).'.html' -eq 'HarborBrowser.HTML'))
 Check 'fixture registration is outside active Windows browser keys' ($cap.StartsWith('HKCU:\Software\Harbor057Fixtures\'))
 [IO.File]::WriteAllText((Join-Path $profile 'bookmarks.json'),'[{"Title":"Owned bookmark","Url":"https://owned.invalid/"}]')
 [IO.File]::WriteAllText((Join-Path $profile 'session.json'),'{"Tabs":[{"Url":"https://owned.invalid/recover"}]}')
 [IO.File]::WriteAllText((Join-Path $profile 'browser-protection.txt'),'on')
 [IO.File]::WriteAllText((Join-Path $profile 'proton-preview.txt'),'on')
 $before=@{};Get-ChildItem $profile -File | Where-Object {$_.Name -ne 'profile.lock'} | ForEach-Object {$before[$_.Name]=(Get-FileHash $_.FullName).Hash}
 Check 'native stable fixture upgrades the same installation' ((Run-Setup $second $install 'upgrade') -eq 0)
 foreach($name in $before.Keys){Check ('upgrade preserves '+$name) ((Get-FileHash -LiteralPath (Join-Path $profile $name)).Hash -eq $before[$name])}
 $snapshots=@(Get-ChildItem (Join-Path $profile 'UpgradeBackups') -Directory)
 Check 'upgrade produces a profile snapshot' ($snapshots.Count -gt 0)
 foreach($name in $before.Keys){Check ('snapshot includes '+$name) (@($snapshots | Where-Object {Test-Path -LiteralPath (Join-Path $_.FullName $name)}).Count -gt 0)}
 $lock=[IO.File]::Open((Join-Path $profile 'profile.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
 try{Check 'busy profile blocks native upgrade' ((Run-Setup $second $install 'busy-profile') -ne 0);Check 'busy profile blocks native uninstall' ((Run-Setup $uninstall $install 'busy-uninstall') -ne 0)}finally{$lock.Dispose()}
 Check 'blocked update preserves the installed executable' ((Get-FileHash -LiteralPath (Join-Path $install 'HarborBrowser.exe')).Hash -eq $appHash)
 $lock=[IO.File]::Open((Join-Path $profile 'bookmarks.json'),[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
 try{Check 'unreadable backup input blocks native upgrade' ((Run-Setup $second $install 'backup-failure') -ne 0)}finally{$lock.Dispose()}
 Check 'failed backup does not replace installed files' ((Get-FileHash -LiteralPath (Join-Path $install 'HarborBrowser.exe')).Hash -eq $appHash)
 Check 'native uninstall completes' ((Run-Setup $uninstall $install 'uninstall') -eq 0)
 Check 'uninstall removes Harbor executable' (!(Test-Path -LiteralPath (Join-Path $install 'HarborBrowser.exe')))
 Check 'uninstall removes the installed update launcher' (!(Test-Path -LiteralPath (Join-Path $install 'HarborUpdateLauncher.exe')))
 foreach($name in $before.Keys){Check ('uninstall keeps '+$name) ((Get-FileHash -LiteralPath (Join-Path $profile $name)).Hash -eq $before[$name])}
 Check 'uninstall removes only fixture capabilities' (!(Test-Path -LiteralPath $cap))
 Check 'uninstall removes fixture association classes' (!(Test-Path -LiteralPath ('HKCU:\'+$regPrefix+'\Classes\HarborBrowser.URL')))
 $foreign=Join-Path $owned 'ForeignApp';New-Item -ItemType Directory -Path $foreign | Out-Null
 Set-Content -LiteralPath (Join-Path $foreign 'HarborBrowser.exe') -Value 'Owned foreign sentinel' -Encoding ascii
 $foreignHash=(Get-FileHash -LiteralPath (Join-Path $foreign 'HarborBrowser.exe')).Hash
 Check 'installer rejects an unrelated Harbor destination' ((Run-Setup $first $foreign 'foreign-destination') -ne 0)
 Check 'unrelated destination sentinel survives' ((Get-FileHash -LiteralPath (Join-Path $foreign 'HarborBrowser.exe')).Hash -eq $foreignHash)
 $legacy=Join-Path $owned 'Legacy053';New-Item -ItemType Directory -Path $legacy | Out-Null
 $legacyApp=Join-Path $root 'IntegrationTests\Fixtures\Legacy\HarborBrowser-0.5.3.exe'
 Check 'legacy upgrade fixture matches the archived 0.5.3 release' ((Get-FileHash -LiteralPath $legacyApp).Hash -eq 'BF36D03382CA42EF331F400768DE354DA88928EABDD5AD7073372E48E6121EF6')
 Copy-Item -LiteralPath $legacyApp -Destination (Join-Path $legacy 'HarborBrowser.exe')
 Check 'stable installer recognizes markerless verified 0.5.3' ((Run-Setup $first $legacy 'legacy-upgrade') -eq 0)
 Check 'legacy upgrade installs the tested 0.6.0 executable' ((Get-FileHash -LiteralPath (Join-Path $legacy 'HarborBrowser.exe')).Hash -eq $appHash)
 Check 'legacy upgrade adds the stable channel marker' ((Get-Content -LiteralPath (Join-Path $legacy 'harbor-release-channel.txt') -Raw).Trim() -eq 'HarborStable')
 foreach($name in $before.Keys){Check ('legacy upgrade preserves '+$name) ((Get-FileHash -LiteralPath (Join-Path $profile $name)).Hash -eq $before[$name])}
 Check 'legacy fixture uninstall succeeds' ((Run-Setup (Join-Path $legacy 'unins000.exe') $legacy 'legacy-remove') -eq 0)
 $stable=Join-Path $owned 'Stable057';New-Item -ItemType Directory -Path $stable | Out-Null
 $previousApp=Join-Path $root 'IntegrationTests\Fixtures\Legacy\HarborBrowser-0.5.7.exe'
 Check 'stable upgrade fixture matches verified 0.5.7' ((Get-FileHash -LiteralPath $previousApp).Hash -eq '349A49B8B835C9F6BE206312EE19BDDF859EEDFB1360FDEAB7373C9ACB4A122B')
 Copy-Item -LiteralPath $previousApp -Destination (Join-Path $stable 'HarborBrowser.exe')
 Set-Content -LiteralPath (Join-Path $stable 'harbor-release-channel.txt') -Value 'HarborStable' -Encoding ascii
 Check 'native setup upgrades stable 0.5.7 to 0.6.0' ((Run-Setup $first $stable 'stable057-upgrade') -eq 0)
 Check 'stable 0.5.7 upgrade installs tested 0.6.0 payload' ((Get-FileHash -LiteralPath (Join-Path $stable 'HarborBrowser.exe')).Hash -eq $appHash)
 foreach($name in $before.Keys){Check ('stable 0.5.7 upgrade preserves '+$name) ((Get-FileHash -LiteralPath (Join-Path $profile $name)).Hash -eq $before[$name])}
 Check 'stable upgrade fixture uninstall succeeds' ((Run-Setup (Join-Path $stable 'unins000.exe') $stable 'stable057-remove') -eq 0)
 foreach($case in @(
  @{Name='Present';Net=0;Web=0;After='1';Expect=0;Calls=0;Files=$true},
  @{Name='Success';Net=0;Web=0;After='1';Expect=0;Calls=2;Files=$true},
  @{Name='NetRestart';Net=3010;Web=0;After='1';Expect=3010;Calls=2;Files=$true},
  @{Name='WebRestart';Net=0;Web=3010;After='1';Expect=3010;Calls=2;Files=$true},
  @{Name='Net1641';Net=1641;Web=0;After='1';Expect=3010;Calls=2;Files=$true},
  @{Name='NetCancel';Net=1602;Web=0;After='1';Expect=-1;Calls=1;Files=$false},
  @{Name='NetFailure';Net=1603;Web=0;After='1';Expect=-1;Calls=1;Files=$false},
  @{Name='WebFailure';Net=0;Web=1603;After='1';Expect=-1;Calls=2;Files=$false},
  @{Name='FalseSuccess';Net=0;Web=0;After='0';Expect=-1;Calls=1;Files=$false}
 )){
  $target=Join-Path $owned $case.Name;$trace=Join-Path $owned ($case.Name+'-trace.txt')
  $env:HARBOR057_TRACE=$trace;$env:HARBOR057_PRESENT=if($case.Name -eq 'Present'){'1'}else{'0'}
  $env:HARBOR057_NET_CODE=[string]$case.Net;$env:HARBOR057_WEB_CODE=[string]$case.Web;$env:HARBOR057_NET_AFTER=$case.After;$env:HARBOR057_WEB_AFTER='1'
  $exit=Run-Setup $first $target $case.Name
  Check ($case.Name+' propagates helper/restart status') (($case.Expect -eq -1 -and $exit -ne 0) -or $exit -eq $case.Expect)
  Check ($case.Name+' gates application copying') ((Test-Path -LiteralPath (Join-Path $target 'HarborBrowser.exe')) -eq $case.Files)
  $calls=@();if(Test-Path -LiteralPath $trace){$calls=@(Get-Content -LiteralPath $trace)}
  Check ($case.Name+' invokes only expected fake helpers') ($calls.Count -eq $case.Calls)
  if($case.Calls -gt 0){Check ($case.Name+' requests no automatic .NET restart') ($calls[0] -eq 'ndp48-x86-x64-allos-enu.exe|/q /norestart')}
  if($case.Calls -eq 2){Check ($case.Name+' requests silent WebView installation') ($calls[1] -eq 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe|/silent /install')}
  $remove=Join-Path $target 'unins000.exe';if(Test-Path -LiteralPath $remove){Check ($case.Name+' cleanup succeeds') ((Run-Setup $remove $target ($case.Name+'-remove')) -eq 0)}
 }
 Check 'HTTP/HTTPS default choices are unchanged' ((Choices) -eq $choicesBefore)
 $passed=$true
}finally{
 foreach($key in $vars){[Environment]::SetEnvironmentVariable($key,$saved[$key])}
 if(Test-Path -LiteralPath $uninstall){[void](Run-Setup $uninstall $install 'cleanup')}
}
[pscustomobject]@{UpdateLauncherSHA256=(Get-FileHash -LiteralPath (Join-Path $payload 'HarborUpdateLauncher.exe')).Hash;Passed=$passed;Checks=$checks;AppSHA256=(Get-FileHash -LiteralPath (Join-Path $payload 'HarborBrowser.exe')).Hash;TemplateSHA256=(Get-FileHash -LiteralPath (Join-Path $root 'Installer\HarborBrowser.iss')).Hash;RegistrySHA256=(Get-FileHash -LiteralPath (Join-Path $root 'Installer\BrowserRegistry.iss')).Hash;Evidence=$owned;RealVendorPackagesExecuted=$false;UserProfilesAccessed=$false;DefaultsUnchanged=$true;RecordedUtc=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'INSTALLER-VERIFICATION.json') -Encoding utf8
Write-Output ('ALL PASSED: '+$checks+' isolated native stable installer/prerequisite checks.')

