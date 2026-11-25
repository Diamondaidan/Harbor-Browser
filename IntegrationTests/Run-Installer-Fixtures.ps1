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
$fixtureId='{9A017D24-1942-47F7-A581-3B19884F0530}'
$uninstall=Join-Path $installRoot 'unins000.exe'
function Check([string]$name,[bool]$value){if(!$value){throw $name};Write-Output ('PASS '+$name)}
function Run-Silent([string]$file,[string[]]$arguments){$run=Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru;if($run.ExitCode -ne 0){throw "Fixture process failed: $($run.ExitCode)"}}
function Default-Choices {
    $state=@{}
    foreach($scheme in @('http','https')){$item=Get-ItemProperty -LiteralPath ('HKCU:\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\'+$scheme+'\UserChoice') -ErrorAction SilentlyContinue;$state[$scheme]=@{ProgId=$item.ProgId;Hash=$item.Hash}}
    return ($state|ConvertTo-Json -Compress)
}
$choicesBefore=Default-Choices
try{
    foreach($version in @('0.5.2','0.5.3')){
        $origin=Join-Path $parent ('HarborBrowser-'+$version)
        $template=[IO.File]::ReadAllText((Join-Path $origin 'Installer/HarborBrowser.iss'))
        $template=$template.Replace('{8D9A0F4C-2E3B-4F64-9C12-8B3F1C0E74A1}',$fixtureId)
        $template=$template.Replace('#define AppName "Harbor Browser"','#define AppName "Harbor Installer Fixture 053"')
        $template=$template.Replace('PrivilegesRequired=admin','PrivilegesRequired=lowest').Replace('CloseApplications=yes','CloseApplications=no')
        $template=$template.Replace('#define BrowserRegistryRoot "HKLM"','#define BrowserRegistryRoot "HKCU"').Replace('#define BrowserClient "HarborBrowser"','#define BrowserClient "HarborFixture053"').Replace('#define BrowserRegisteredName "Harbor Browser"','#define BrowserRegisteredName "Harbor Browser Fixture 053"')
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
        if($template.Contains('8D9A0F4C') -or $template.Contains("ExpandConstant('{localappdata}\HarborBrowser')") -or $template.Contains('[Icons]') -or $template.Contains('ExtractTemporaryFile') -or $template.Contains('#define BrowserRegistryRoot "HKLM"') -or $template.Contains('#define BrowserClient "HarborBrowser"')){throw 'Fixture isolation checks failed.'}
        $iss=Join-Path $fixtureRoot ('Fixture-'+$version+'.iss');[IO.File]::WriteAllText($iss,$template)
        & $compiler ("/DAppVersion="+$version) $iss *> (Join-Path $fixtureRoot ('compile-'+$version+'.log'))
        if($LASTEXITCODE -ne 0){Get-Content (Join-Path $fixtureRoot ('compile-'+$version+'.log')) -Tail 15;throw 'Fixture compile failed.'}
        $setup=Join-Path $fixtureRoot ('Fixture-'+$version+'.exe')
        Run-Silent $setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',('/DIR="'+$installRoot+'"'),('/LOG="'+(Join-Path $fixtureRoot ('install-'+$version+'.log'))+'"'))
        Check ($version+' fixture installs matching release payload') ((Get-FileHash (Join-Path $installRoot 'HarborBrowser.exe')).Hash -eq (Get-FileHash (Join-Path $origin 'dist/payload/HarborBrowser.exe')).Hash)
        if($version -eq '0.5.3'){
            $payload=Join-Path $origin 'dist/payload'
            $mismatches=@(Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object {
                $relative=$_.FullName.Substring($payload.Length+1)
                $installed=Join-Path $installRoot $relative
                !(Test-Path -LiteralPath $installed) -or (Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $installed).Hash
            })
            Check 'installed files match every staged release payload file' ($mismatches.Count -eq 0)
            Check 'installer includes matching Tor source and provenance' ((Test-Path -LiteralPath (Join-Path $installRoot 'Licensing/tor-0.4.9.16-source.tar.gz')) -and (Test-Path -LiteralPath (Join-Path $installRoot 'Licensing/TOR-PROVENANCE.md')))
            Check 'installer includes Tor component license notices' ((Get-ChildItem -LiteralPath (Join-Path $installRoot 'Assets/BrowserVpn/TorRuntime/docs') -File).Count -ge 4)
            $base='HKCU:\Software\Clients\StartMenuInternet\HarborFixture053\Capabilities'
            Check 'installer advertises HTTP and HTTPS browser capabilities' (((Get-ItemProperty ($base+'\URLAssociations')).http -eq 'HarborFixture053.URL') -and ((Get-ItemProperty ($base+'\URLAssociations')).https -eq 'HarborFixture053.URL'))
            Check 'installer registers its browser name with Windows' ((Get-ItemProperty -LiteralPath 'HKCU:\Software\RegisteredApplications').'Harbor Browser Fixture 053' -eq 'Software\Clients\StartMenuInternet\HarborFixture053\Capabilities')
            $command=(Get-Item -LiteralPath 'HKCU:\Software\Classes\HarborFixture053.URL\shell\open\command').GetValue('')
            Check 'Windows protocol command quotes executable and target' ($command -eq ('"'+(Join-Path $installRoot 'HarborBrowser.exe')+'" -- "%1"'))
            Check 'installer advertises HTML file handling' (((Get-ItemProperty ($base+'\FileAssociations')).'.html' -eq 'HarborFixture053.HTML') -and (Get-ItemProperty -LiteralPath 'HKCU:\Software\Classes\.html\OpenWithProgids').PSObject.Properties.Name.Contains('HarborFixture053.HTML'))
        }
        if($version -eq '0.5.2'){
            [IO.File]::WriteAllText((Join-Path $profileRoot 'settings.json'),'{"ThemeMode":"Dark"}')
            [IO.File]::WriteAllText((Join-Path $profileRoot 'bookmarks.json'),'[{"Title":"Keep this bookmark","Url":"https://example.com/"}]')
            [IO.File]::WriteAllText((Join-Path $profileRoot 'modules.json'),'{}')
            [IO.File]::WriteAllText((Join-Path $profileRoot 'module-permissions.json'),'{}')
            [IO.File]::WriteAllText((Join-Path $profileRoot 'data-version.txt'),'9')
            [IO.File]::WriteAllText((Join-Path $profileRoot 'tor-preview.txt'),'off')
            [IO.File]::WriteAllText((Join-Path $profileRoot 'run-state.json'),'{"CleanExit":true}')
            $before=@{};Get-ChildItem -LiteralPath $profileRoot -File | ForEach-Object{$before[$_.Name]=(Get-FileHash $_.FullName).Hash}
        }else{
            foreach($name in $before.Keys){Check ('upgrade preserves '+$name) ((Get-FileHash (Join-Path $profileRoot $name)).Hash -eq $before[$name])}
            $snapshots=@(Get-ChildItem -LiteralPath (Join-Path $profileRoot 'UpgradeBackups') -Directory)
            Check 'upgrade creates a profile snapshot' ($snapshots.Count -gt 0)
            Check 'upgrade snapshot includes saved Tor choice' (Test-Path -LiteralPath (Join-Path $snapshots[0].FullName 'tor-preview.txt'))
            Check 'upgrade snapshot includes startup state' (Test-Path -LiteralPath (Join-Path $snapshots[0].FullName 'run-state.json'))
            Check 'upgrade snapshot includes site-access metadata' (Test-Path -LiteralPath (Join-Path $snapshots[0].FullName 'module-permissions.json'))
        }
    }
    Run-Silent $uninstall @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $fixtureRoot 'uninstall.log')+'"'))
    Check 'fixture uninstall removes app executable' (!(Test-Path -LiteralPath (Join-Path $installRoot 'HarborBrowser.exe')))
    Check 'fixture uninstall preserves separate profile' ((Get-FileHash (Join-Path $profileRoot 'bookmarks.json')).Hash -eq $before['bookmarks.json'])
    Check 'fixture uninstall removes only its own registration' (!(Test-Path -LiteralPath ('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\'+$fixtureId+'_is1')))
    Check 'uninstall removes only fixture browser capabilities' (!(Test-Path -LiteralPath 'HKCU:\Software\Clients\StartMenuInternet\HarborFixture053') -and !(Test-Path -LiteralPath 'HKCU:\Software\Classes\HarborFixture053.URL') -and !(Test-Path -LiteralPath 'HKCU:\Software\Classes\HarborFixture053.HTML'))
    Check 'uninstall removes fixture RegisteredApplications value' (!((Get-ItemProperty -LiteralPath 'HKCU:\Software\RegisteredApplications').PSObject.Properties.Name.Contains('Harbor Browser Fixture 053')))
    Check 'installation leaves the user HTTP and HTTPS default choices unchanged' ((Default-Choices) -eq $choicesBefore)
    Write-Output ('Results: '+$fixtureRoot)
}finally{
    if(Test-Path -LiteralPath $uninstall){Run-Silent $uninstall @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')}
}
