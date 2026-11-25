$ErrorActionPreference='Stop'
$releaseRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$fixtureRoot=Join-Path $releaseRoot ('T\links'+(Get-Date -Format 'MMddHHmmss'))
$installRoot=Join-Path $fixtureRoot 'App'
$shortcutRoot=Join-Path $fixtureRoot 'Links'
$profileRoot=Join-Path $fixtureRoot 'Profile'
New-Item -ItemType Directory -Path $fixtureRoot,$profileRoot -Force | Out-Null
$fixtureId='{FF710D81-8527-4A48-9CE2-99200B9F0540}'
$template=[IO.File]::ReadAllText((Join-Path $releaseRoot 'Installer\HarborBrowser.iss'))
$template=$template.Replace('{8D9A0F4C-2E3B-4F64-9C12-8B3F1C0E74A1}',$fixtureId)
$template=$template.Replace('#define AppName "Harbor Browser"','#define AppName "Harbor Shortcut Fixture 054"')
$template=$template.Replace('PrivilegesRequired=admin','PrivilegesRequired=lowest').Replace('CloseApplications=yes','CloseApplications=no')
$template=$template.Replace('#define BrowserRegistryRoot "HKLM"','#define BrowserRegistryRoot "HKCU"').Replace('#define BrowserClient "HarborBrowser"','#define BrowserClient "HarborShortcutFixture054"').Replace('#define BrowserRegisteredName "Harbor Browser"','#define BrowserRegisteredName "Harbor Shortcut Fixture 054"')
$template=$template.Replace('DefaultDirName={autopf}\Harbor Browser','DefaultDirName='+$installRoot)
$template=$template.Replace('#define PayloadDir "..\dist\payload"','#define PayloadDir "'+(Join-Path $releaseRoot 'dist\payload')+'"')
$template=$template.Replace('SetupIconFile=assets\HarborBrowser.ico','SetupIconFile='+(Join-Path $releaseRoot 'Installer\assets\HarborBrowser.ico'))
$template=$template.Replace('OutputDir=..\dist','OutputDir='+$fixtureRoot).Replace('OutputBaseFilename=HarborBrowserSetup-{#AppVersion}{#ReleaseSuffix}','OutputBaseFilename=ShortcutFixture')
$template=$template.Replace('Name: "{group}\Harbor Browser"','Name: "'+$shortcutRoot+'\Start Menu\Harbor Browser"').Replace('Name: "{autodesktop}\Harbor Browser"','Name: "'+$shortcutRoot+'\Desktop\Harbor Browser"').Replace('AppUserModelID: "HarborBrowser.Desktop"','AppUserModelID: "HarborBrowser.ShortcutFixture054"')
$template=$template.Replace("  DataDir := ExpandConstant('{localappdata}\HarborBrowser');","  DataDir := '"+$profileRoot.Replace("'","''")+"';")
$template=[regex]::Replace($template,'(?ms)^\[Run\].*?(?=^\[Registry\])','')
$template=[regex]::Replace($template,'(?m)^Source:.*RedistDir.*\r?\n','')
$prepare=@'
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  NeedsRestart := False;
  if not IsDotNet48OrLaterInstalled or not IsWebView2RuntimeInstalled then
  begin
    Result := 'This fixture requires existing prerequisites.';
    Exit;
  end;
  Result := BackupExistingProfile;
end;
'@
$template=[regex]::Replace($template,'function PrepareToInstall[\s\S]+$',$prepare)
if($template.Contains('8D9A0F4C') -or $template.Contains('{group}') -or $template.Contains('{autodesktop}') -or $template.Contains('[Run]') -or $template.Contains('ExtractTemporaryFile') -or $template.Contains("ExpandConstant('{localappdata}\HarborBrowser')") -or $template.Contains('#define BrowserRegistryRoot "HKLM"')){throw 'Shortcut fixture isolation failed.'}
$iss=Join-Path $fixtureRoot 'ShortcutFixture.iss';[IO.File]::WriteAllText($iss,$template)
$compiler=Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'
& $compiler /Qp ("/DAppVersion="+(Get-Content -LiteralPath (Join-Path $releaseRoot 'VERSION.txt') -Raw).Trim()) $iss *> (Join-Path $fixtureRoot 'compile.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $fixtureRoot 'compile.log') -Tail 12;throw 'Shortcut fixture compile failed.'}
$script:checks=0
function Check([string]$name,[bool]$value){if(!$value){throw $name};$script:checks++;Write-Output ('PASS '+$name)}
function Defaults {
  $state=@{};foreach($scheme in @('http','https')){$item=Get-ItemProperty -LiteralPath ('HKCU:\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\'+$scheme+'\UserChoice') -ErrorAction SilentlyContinue;$state[$scheme]=@{ProgId=$item.ProgId;Hash=$item.Hash}};return ($state|ConvertTo-Json -Compress)
}
function Uninstall {
  $uninstall=Join-Path $installRoot 'unins000.exe'
  if(Test-Path -LiteralPath $uninstall){$run=Start-Process -FilePath $uninstall -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -Wait -PassThru;if($run.ExitCode -ne 0){throw 'Shortcut fixture uninstall failed'}}
}
$before=Defaults
$wsh=New-Object -ComObject WScript.Shell
$shell=New-Object -ComObject Shell.Application
try {
 foreach($desktop in @($false,$true)){
  $label=if($desktop){'Desktop selected'}else{'Default shortcuts'}
  $arguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$installRoot+'"'),('/LOG="'+(Join-Path $fixtureRoot ('install-'+$desktop+'.log'))+'"'))
  if($desktop){$arguments+='/TASKS=desktopicon'}
  $run=Start-Process -FilePath (Join-Path $fixtureRoot 'ShortcutFixture.exe') -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
  Check ($label+' setup succeeds') ($run.ExitCode -eq 0)
  $menu=Join-Path $shortcutRoot 'Start Menu\Harbor Browser.lnk';$desk=Join-Path $shortcutRoot 'Desktop\Harbor Browser.lnk'
  Check ($label+' creates Start menu link') (Test-Path -LiteralPath $menu)
  Check ($label+' honors optional desktop task') ((Test-Path -LiteralPath $desk) -eq $desktop)
  foreach($linkPath in @($menu,$desk) | Where-Object {Test-Path -LiteralPath $_}){
   $link=$wsh.CreateShortcut($linkPath);$kind=Split-Path (Split-Path $linkPath) -Leaf
   Check ($label+' '+$kind+' targets installed app') ($link.TargetPath -eq (Join-Path $installRoot 'HarborBrowser.exe'))
   Check ($label+' '+$kind+' sets working directory') ($link.WorkingDirectory -eq $installRoot)
   $icon=(Join-Path $installRoot 'Branding\HarborBeacon-0.5.4.ico')
   Check ($label+' '+$kind+' uses packaged Beacon icon') ($link.IconLocation.StartsWith($icon,[StringComparison]::OrdinalIgnoreCase) -and (Get-FileHash -LiteralPath $icon).Hash -eq (Get-FileHash -LiteralPath (Join-Path $releaseRoot 'Installer\assets\HarborBrowser.ico')).Hash)
   $folder=$shell.Namespace((Split-Path $linkPath));$item=$folder.ParseName((Split-Path $linkPath -Leaf))
   Check ($label+' '+$kind+' stores application identity') ($item.ExtendedProperty('System.AppUserModel.ID') -eq 'HarborBrowser.ShortcutFixture054')
  }
  Uninstall
  Check ($label+' uninstall removes fixture links') (!(Test-Path -LiteralPath $menu) -and !(Test-Path -LiteralPath $desk))
  Check ($label+' uninstall removes fixture app') (!(Test-Path -LiteralPath (Join-Path $installRoot 'HarborBrowser.exe')))
 }
 Check 'shortcut installs preserve Windows browser defaults' ((Defaults) -eq $before)
 Write-Output ('ALL PASSED: '+$script:checks+' isolated shortcut assertions.')
 Write-Output ('Results: '+$fixtureRoot)
}finally{Uninstall}
