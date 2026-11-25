[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$releaseRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$fixtureRoot=Join-Path $PSScriptRoot ('TestResults\prerequisites-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if(![IO.Path]::GetFullPath($fixtureRoot).StartsWith($releaseRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture escaped release root.'}
$redist=Join-Path $fixtureRoot 'FakeRedist'
$payload=Join-Path $fixtureRoot 'TinyPayload'
New-Item -ItemType Directory -Path $redist,(Join-Path $payload 'Branding') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $releaseRoot 'dist\payload\HarborBrowser.exe') -Destination $payload
Copy-Item -LiteralPath (Join-Path $releaseRoot 'dist\payload\Branding\HarborBeacon.ico') -Destination (Join-Path $payload 'Branding')
$stub=@'
using System;
using System.IO;
using System.Reflection;
class PrerequisiteStub {
  static int Main(string[] args) {
    string name=Path.GetFileName(Assembly.GetExecutingAssembly().Location);
    File.AppendAllText(Environment.GetEnvironmentVariable("HARBOR_FIXTURE_TRACE"),name+"|"+string.Join(" ",args)+Environment.NewLine);
    return int.Parse(Environment.GetEnvironmentVariable(name.StartsWith("ndp")?"HARBOR_FIXTURE_DOTNET_CODE":"HARBOR_FIXTURE_WEBVIEW_CODE"));
  }
}
'@
$stubSource=Join-Path $fixtureRoot 'PrerequisiteStub.cs'
[IO.File]::WriteAllText($stubSource,$stub)
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:exe ('/out:'+(Join-Path $redist 'ndp48-x86-x64-allos-enu.exe')) $stubSource
if($LASTEXITCODE -ne 0){throw 'Stub build failed.'}
Copy-Item -LiteralPath (Join-Path $redist 'ndp48-x86-x64-allos-enu.exe') -Destination (Join-Path $redist 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe')
$compiler=Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'
$checks=0
function Check([string]$name,[bool]$value){if(!$value){throw $name};$script:checks++;Write-Output ('PASS '+$name)}
$template=[IO.File]::ReadAllText((Join-Path $releaseRoot 'Installer\HarborBrowser.iss'))
$template=$template.Replace('{8D9A0F4C-2E3B-4F64-9C12-8B3F1C0E74A1}','{760FABE5-A162-440A-A793-2AC0468F0520}')
$template=$template.Replace('#define AppName "Harbor Browser"','#define AppName "Harbor Prerequisite Fixture 052"')
$template=$template.Replace('PrivilegesRequired=admin','PrivilegesRequired=lowest').Replace('CloseApplications=yes','CloseApplications=no')
$template=$template.Replace('#define PayloadDir "..\dist\payload"','#define PayloadDir "'+$payload+'"').Replace('#define RedistDir "redist"','#define RedistDir "'+$redist+'"')
$template=$template.Replace('SetupIconFile=assets\HarborBrowser.ico','SetupIconFile='+(Join-Path $releaseRoot 'Installer\assets\HarborBrowser.ico'))
$template=$template.Replace('OutputDir=..\dist','OutputDir='+$fixtureRoot).Replace('OutputBaseFilename=HarborBrowserSetup-{#AppVersion}{#ReleaseSuffix}','OutputBaseFilename=PrerequisiteFixture')
$template=[regex]::Replace($template,'(?ms)^\[Icons\].*?(?=^\[Code\])','')
$template=[regex]::Replace($template,'function IsDotNet48OrLaterInstalled:[\s\S]+?(?=function IsUsableVersion)',"function IsDotNet48OrLaterInstalled: Boolean;`r`nbegin`r`n  Result := False;`r`nend;`r`n`r`n")
$template=[regex]::Replace($template,'function IsWebView2RuntimeInstalled:[\s\S]+?(?=function CopyProfileFile)',"function IsWebView2RuntimeInstalled: Boolean;`r`nbegin`r`n  Result := False;`r`nend;`r`n`r`n")
$template=$template.Replace("  DataDir := ExpandConstant('{localappdata}\HarborBrowser');","  DataDir := '"+(Join-Path $fixtureRoot 'EmptyProfile').Replace("'","''")+"';")
$versionChecks=@'

function InitializeSetup: Boolean;
begin
  Result := not IsUsableVersion('') and not IsUsableVersion('0.0.0.0')
    and not IsUsableVersion('invalid') and not IsUsableVersion('156.0.1.0')
    and IsUsableVersion('157.0.4341.46') and IsUsableVersion('158.0.1.0');
end;
'@
$template+=$versionChecks
if($template.Contains('8D9A0F4C') -or $template.Contains('[Registry]') -or $template.Contains('[Run]') -or $template.Contains('[Icons]') -or $template.Contains("ExpandConstant('{localappdata}\HarborBrowser')")){throw 'Prerequisite fixture isolation failed.'}
$iss=Join-Path $fixtureRoot 'PrerequisiteFixture.iss'
[IO.File]::WriteAllText($iss,$template)
& $compiler /Qp $iss *> (Join-Path $fixtureRoot 'compile.log')
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $fixtureRoot 'compile.log') -Tail 15;throw 'Fixture compile failed.'}
$saved=@{};foreach($key in @('HARBOR_FIXTURE_TRACE','HARBOR_FIXTURE_DOTNET_CODE','HARBOR_FIXTURE_WEBVIEW_CODE')){$saved[$key]=[Environment]::GetEnvironmentVariable($key)}
try {
  foreach($case in @(
    @{Name='Success';Net=0;Web=0;Exit=0;Installed=$true;Calls=2},
    @{Name='DotNetRestart';Net=3010;Web=0;Exit=3010;Installed=$true;Calls=2},
    @{Name='WebViewRestart';Net=0;Web=3010;Exit=3010;Installed=$true;Calls=2},
    @{Name='DotNetFailure';Net=1603;Web=0;Exit=-1;Installed=$false;Calls=1},
    @{Name='WebViewFailure';Net=0;Web=1603;Exit=-1;Installed=$false;Calls=2}
  )){
    $target=Join-Path $fixtureRoot $case.Name
    $trace=Join-Path $fixtureRoot ($case.Name+'-trace.txt')
    $env:HARBOR_FIXTURE_TRACE=$trace;$env:HARBOR_FIXTURE_DOTNET_CODE=[string]$case.Net;$env:HARBOR_FIXTURE_WEBVIEW_CODE=[string]$case.Web
    $arguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS','/RESTARTEXITCODE=3010',('/DIR="'+$target+'"'),('/LOG="'+(Join-Path $fixtureRoot ($case.Name+'.log'))+'"'))
    $run=Start-Process -FilePath (Join-Path $fixtureRoot 'PrerequisiteFixture.exe') -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    Check ($case.Name+' handles prerequisite result') (($case.Exit -eq -1 -and $run.ExitCode -ne 0) -or $run.ExitCode -eq $case.Exit)
    if($case.Name -eq 'Success'){Check 'native setup version checks reject missing/old and accept tested/newer WebView2' ($run.ExitCode -eq 0)}
    Check ($case.Name+' gates application copying') ((Test-Path -LiteralPath (Join-Path $target 'HarborBrowser.exe')) -eq $case.Installed)
    $calls=@(Get-Content -LiteralPath $trace)
    Check ($case.Name+' invokes only the expected prerequisite steps') ($calls.Count -eq $case.Calls)
    Check ($case.Name+' passes silent no-restart .NET arguments') ($calls[0] -eq 'ndp48-x86-x64-allos-enu.exe|/q /norestart')
    if($case.Calls -eq 2){Check ($case.Name+' passes silent WebView arguments') ($calls[1] -eq 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe|/silent /install')}
    $uninstall=Join-Path $target 'unins000.exe'
    if(Test-Path -LiteralPath $uninstall){$remove=Start-Process -FilePath $uninstall -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -Wait -PassThru;if($remove.ExitCode -ne 0){throw 'Fixture uninstall failed.'}}
  }
} finally {foreach($key in $saved.Keys){[Environment]::SetEnvironmentVariable($key,$saved[$key])}}
Write-Output ('ALL PASSED: '+$checks+' simulated prerequisite checks. Real vendor packages were not executed.')
Write-Output ('Results: '+$fixtureRoot)
