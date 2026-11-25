param()
$ErrorActionPreference='Stop'
$releaseRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$parent=Split-Path -Parent $releaseRoot
$fixtureRoot=Join-Path $PSScriptRoot ('TestResults/installer-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$installRoot=Join-Path $fixtureRoot 'Installed App'
$profileRoot=Join-Path $fixtureRoot 'Profile'
foreach($path in @($fixtureRoot,$installRoot,$profileRoot)){
    if(![IO.Path]::GetFullPath($path).StartsWith($releaseRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture path escaped this release.'}
}
New-Item -ItemType Directory -Path $fixtureRoot,$profileRoot -Force | Out-Null
$compiler=Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 7/ISCC.exe'
$fixtureId='{9A017D24-1942-47F7-A581-3B19884F0430}'
$uninstall=Join-Path $installRoot 'unins000.exe'
function Check([string]$name,[bool]$value){if(!$value){throw $name};Write-Output ('PASS '+$name)}
function Run-Silent([string]$file,[string[]]$arguments){$run=Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru;if($run.ExitCode -ne 0){throw "Fixture process failed: $($run.ExitCode)"}}
try{
    foreach($version in @('0.4.2','0.4.3')){
        $origin=Join-Path $parent ('HarborBrowser-'+$version)
        $template=[IO.File]::ReadAllText((Join-Path $origin 'Installer/HarborBrowser.iss'))
        $template=$template.Replace('{8D9A0F4C-2E3B-4F64-9C12-8B3F1C0E74A1}',$fixtureId)
        $template=$template.Replace('#define AppName "Harbor Browser"','#define AppName "Harbor Installer Fixture 043"')
        $template=$template.Replace('PrivilegesRequired=admin','PrivilegesRequired=lowest').Replace('CloseApplications=yes','CloseApplications=no')
        $template=$template.Replace('DefaultDirName={autopf}\Harbor Browser','DefaultDirName='+$installRoot)
        $template=$template.Replace('#define PayloadDir "..\dist\payload"','#define PayloadDir "'+(Join-Path $origin 'dist/payload')+'"')
        $template=$template.Replace('SetupIconFile=assets\HarborBrowser.ico','SetupIconFile='+(Join-Path $origin 'Installer/assets/HarborBrowser.ico'))
        $template=$template.Replace('OutputDir=..\dist','OutputDir='+$fixtureRoot).Replace('OutputBaseFilename=HarborBrowserSetup-{#AppVersion}{#ReleaseSuffix}','OutputBaseFilename=Fixture-'+$version)
        $template=$template.Replace("  DataDir := ExpandConstant('{localappdata}\HarborBrowser');","  DataDir := '"+$profileRoot.Replace("'","''")+"';")
        $template=[regex]::Replace($template,'(?ms)^\[Icons\].*?(?=^\[Run\])','')
        $template=[regex]::Replace($template,'(?m)^Source:.*RedistDir.*\r?\n','')
        # Exercise real profile-backup logic with existing prerequisites only.
        # Fixture installers cannot run prerequisite installers or launch Harbor.
        $prepare=@'
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  NeedsRestart := False;
  if not IsDotNet48OrLaterInstalled or not IsWebView2RuntimeInstalled then
  begin
    Result := 'Installer fixture requires already installed prerequisites.';
    Exit;
  end;
  Result := BackupExistingProfile;
end;
'@
        $template=[regex]::Replace($template,'function PrepareToInstall[\s\S]+$',$prepare)
        if($template.Contains('8D9A0F4C') -or $template.Contains("ExpandConstant('{localappdata}\HarborBrowser')") -or $template.Contains('[Icons]') -or $template.Contains('ExtractTemporaryFile')){throw 'Fixture isolation checks failed.'}
        $iss=Join-Path $fixtureRoot ('Fixture-'+$version+'.iss');[IO.File]::WriteAllText($iss,$template)
        & $compiler $iss *> (Join-Path $fixtureRoot ('compile-'+$version+'.log'))
        if($LASTEXITCODE -ne 0){Get-Content (Join-Path $fixtureRoot ('compile-'+$version+'.log')) -Tail 15;throw 'Fixture compile failed.'}
        $setup=Join-Path $fixtureRoot ('Fixture-'+$version+'.exe')
        Run-Silent $setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',('/DIR="'+$installRoot+'"'),('/LOG="'+(Join-Path $fixtureRoot ('install-'+$version+'.log'))+'"'))
        Check ($version+' fixture installs matching release payload') ((Get-FileHash (Join-Path $installRoot 'HarborBrowser.exe')).Hash -eq (Get-FileHash (Join-Path $origin 'dist/payload/HarborBrowser.exe')).Hash)
        if($version -eq '0.4.2'){
            [IO.File]::WriteAllText((Join-Path $profileRoot 'settings.json'),'{"ThemeMode":"Dark"}')
            [IO.File]::WriteAllText((Join-Path $profileRoot 'bookmarks.json'),'[{"Title":"Keep this bookmark","Url":"https://example.com/"}]')
            [IO.File]::WriteAllText((Join-Path $profileRoot 'modules.json'),'{}')
            [IO.File]::WriteAllText((Join-Path $profileRoot 'module-permissions.json'),'{}')
            [IO.File]::WriteAllText((Join-Path $profileRoot 'data-version.txt'),'8')
            $before=@{};Get-ChildItem -LiteralPath $profileRoot -File | ForEach-Object{$before[$_.Name]=(Get-FileHash $_.FullName).Hash}
        }else{
            foreach($name in $before.Keys){Check ('upgrade preserves '+$name) ((Get-FileHash (Join-Path $profileRoot $name)).Hash -eq $before[$name])}
            $snapshots=@(Get-ChildItem -LiteralPath (Join-Path $profileRoot 'UpgradeBackups') -Directory)
            Check 'upgrade creates a profile snapshot' ($snapshots.Count -gt 0)
            Check 'upgrade snapshot includes site-access metadata' (Test-Path -LiteralPath (Join-Path $snapshots[0].FullName 'module-permissions.json'))
        }
    }
    Run-Silent $uninstall @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $fixtureRoot 'uninstall.log')+'"'))
    Check 'fixture uninstall removes app executable' (!(Test-Path -LiteralPath (Join-Path $installRoot 'HarborBrowser.exe')))
    Check 'fixture uninstall preserves separate profile' ((Get-FileHash (Join-Path $profileRoot 'bookmarks.json')).Hash -eq $before['bookmarks.json'])
    Check 'fixture uninstall removes only its own registration' (!(Test-Path -LiteralPath ('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\'+$fixtureId+'_is1')))
    Write-Output ('Results: '+$fixtureRoot)
}finally{
    if(Test-Path -LiteralPath $uninstall){Run-Silent $uninstall @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')}
}
