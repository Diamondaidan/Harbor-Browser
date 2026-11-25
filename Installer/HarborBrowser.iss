; Stable Harbor 0.6.0. Preserve the established installation and profile.
#define AppExeName "HarborBrowser.exe"
#ifndef AppVersion
#define AppVersion "0.6.0"
#endif
#ifndef Candidate
#define Candidate "release"
#endif
#ifndef BuildRevision
#define BuildRevision 0
#endif
#ifndef PayloadDir
#define PayloadDir "..\dist\Harbor-0.6.0-release"
#endif
#ifndef RedistDir
#define RedistDir "redist"
#endif
#ifndef OutputFolder
#define OutputFolder "..\dist"
#endif
#ifndef ReleaseAppId
#define ReleaseAppId "{{8D9A0F4C-2E3B-4F64-9C12-8B3F1C0E74A1}"
#endif
#ifndef AppName
#define AppName "Harbor Browser"
#endif
#ifndef RegistryPrefix
#define RegistryPrefix "Software"
#endif
#ifdef FixtureBuild
#ifndef FixtureInstallDir
#error FixtureInstallDir is required.
#endif
#ifndef FixtureProfileDir
#error FixtureProfileDir is required.
#endif
#endif

[Setup]
AppId={#ReleaseAppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Harbor Browser Project
#ifdef FixtureBuild
DefaultDirName={#FixtureInstallDir}
#else
DefaultDirName={autopf}\Harbor Browser
#endif
UsePreviousAppDir=yes
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\Branding\HarborBeacon-0.6.0.ico
SetupIconFile=assets\HarborBrowser.ico
OutputDir={#OutputFolder}
OutputBaseFilename=HarborBrowserSetup-{#AppVersion}-{#Candidate}
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
#ifdef FixtureBuild
PrivilegesRequired=lowest
#else
PrivilegesRequired=admin
#endif
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
CloseApplications=no
RestartApplications=no
SetupLogging=yes
ChangesAssociations=yes
VersionInfoVersion={#AppVersion}.{#BuildRevision}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadDir}\Branding\HarborBeacon.ico"; DestDir: "{app}\Branding"; DestName: "HarborBeacon-0.6.0.ico"; Flags: ignoreversion
Source: "{#RedistDir}\ndp48-x86-x64-allos-enu.exe"; Flags: dontcopy
Source: "{#RedistDir}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; Flags: dontcopy

#ifndef FixtureBuild
[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\HarborBrowser.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Branding\HarborBeacon-0.6.0.ico"; AppUserModelID: "HarborBrowser.Desktop"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\HarborBrowser.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Branding\HarborBeacon-0.6.0.ico"; AppUserModelID: "HarborBrowser.Desktop"; Tasks: desktopicon
[Run]
Filename: "{app}\HarborBrowser.exe"; Description: "Open Harbor Browser"; Flags: postinstall nowait skipifsilent unchecked; Check: CanLaunch
#endif
#include "BrowserRegistry.iss"

[Code]
var
  ProfileHandle: THandle;
  RestartRequired, TriedNet, TriedWeb: Boolean;

function CreateFileW(Name: String; Access, Share: Cardinal; Security: Integer;
  Creation, Attributes: Cardinal; Template: THandle): THandle;
  external 'CreateFileW@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

function ProfileDir: String;
begin
#ifdef FixtureBuild
  Result := '{#FixtureProfileDir}';
#else
  Result := ExpandConstant('{localappdata}\HarborBrowser');
#endif
end;

procedure ReleaseProfile;
begin
  if (ProfileHandle <> 0) and (ProfileHandle <> THandle(-1)) then CloseHandle(ProfileHandle);
  ProfileHandle := 0;
end;

function LockProfile: Boolean;
begin
  Result := True;
  if not DirExists(ProfileDir) then Exit;
  ProfileHandle := CreateFileW(ProfileDir + '\profile.lock', $C0000000, 0, 0, 4, $80, 0);
  Result := ProfileHandle <> THandle(-1);
  if not Result then ProfileHandle := 0;
end;

function IsDotNetInstalled: Boolean;
var ReleaseValue: Cardinal;
begin
#ifdef FixtureBuild
  if TriedNet then Result := GetEnv('HARBOR057_NET_AFTER') = '1'
  else Result := GetEnv('HARBOR057_PRESENT') = '1';
#else
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue) and (ReleaseValue >= 528040);
#endif
end;

function UsableWebView(const Text: String): Boolean;
var P: Integer;
begin
  P := Pos('.', Text);
  Result := (P > 1) and (StrToIntDef(Copy(Text, 1, P - 1), 0) >= 157);
end;

function IsWebViewInstalled: Boolean;
var V: String;
begin
#ifdef FixtureBuild
  if TriedWeb then Result := GetEnv('HARBOR057_WEB_AFTER') = '1'
  else Result := GetEnv('HARBOR057_PRESENT') = '1';
#else
  Result := (RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', V) and UsableWebView(V));
  if not Result then Result := RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', V) and UsableWebView(V);
#endif
end;

function Prerequisite(const Name, Args: String; Elevate: Boolean): String;
var Code: Integer; Started: Boolean;
begin
  ExtractTemporaryFile(Name);
#ifdef FixtureBuild
  Started := Exec(ExpandConstant('{tmp}\') + Name, Args, '', SW_HIDE, ewWaitUntilTerminated, Code);
#else
  if Elevate then Started := ShellExec('runas', ExpandConstant('{tmp}\') + Name, Args, '', SW_HIDE, ewWaitUntilTerminated, Code)
  else Started := Exec(ExpandConstant('{tmp}\') + Name, Args, '', SW_HIDE, ewWaitUntilTerminated, Code);
#endif
  Log(Name + ' exit code: ' + IntToStr(Code));
  Result := '';
  if not Started then Result := 'Could not start the required Microsoft installer. No Harbor files were updated.'
  else if (Code = 3010) or (Code = 1641) then RestartRequired := True
  else if Code <> 0 then Result := 'Microsoft prerequisite failed or was canceled (code ' + IntToStr(Code) + '). No Harbor files were updated.';
end;

function BackupProfile: String;
var Names: TArrayOfString; I, Suffix: Integer; Folder, Base: String;
begin
  Result := '';
  if not DirExists(ProfileDir) then Exit;
  Base := ProfileDir + '\UpgradeBackups\' + GetDateTimeString('yyyymmdd-hhnnss', '-', ':');
  Folder := Base; Suffix := 0;
  while DirExists(Folder) do begin Suffix := Suffix + 1; Folder := Base + '-' + IntToStr(Suffix); end;
  if not ForceDirectories(Folder) then begin Result := 'Could not create the Harbor profile backup.'; Exit; end;
  Names := ['settings.json','bookmarks.json','history.json','session.json','permissions.json','session-recovery.json','run-state.json','data-version.txt','modules.json','module-permissions.json','proton-preview.txt','browser-protection.txt'];
  for I := 0 to GetArrayLength(Names) - 1 do begin
    if FileExists(ProfileDir + '\' + Names[I]) then
      if not CopyFile(ProfileDir + '\' + Names[I], Folder + '\' + Names[I], False) then begin Result := 'The Harbor profile could not be backed up. No Harbor files were updated.'; Exit; end;
    if FileExists(ProfileDir + '\' + Names[I] + '.bak') then
      if not CopyFile(ProfileDir + '\' + Names[I] + '.bak', Folder + '\' + Names[I] + '.bak', False) then begin Result := 'A Harbor backup file could not be copied. No Harbor files were updated.'; Exit; end;
  end;
  Log('Harbor profile snapshot created. Authentication databases were not copied.');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Marker: AnsiString;
begin
  Result := ''; NeedsRestart := False;
  if FileExists(ExpandConstant('{app}\HarborBrowser.exe')) then begin
    if not LoadStringFromFile(ExpandConstant('{app}\harbor-release-channel.txt'), Marker) or (Trim(String(Marker)) <> 'HarborStable') then begin
      { The installed 0.5.3 release predates channel markers. Recognize only its
        exact archived application hash, never a filename or version string. }
      if CompareText(GetSHA256OfFile(ExpandConstant('{app}\HarborBrowser.exe')),
        'BF36D03382CA42EF331F400768DE354DA88928EABDD5AD7073372E48E6121EF6') <> 0 then begin
        Result := 'This destination is not a recognized Harbor release. Keep it unchanged and install to another folder.'; Exit;
      end;
      Log('Recognized the archived Harbor 0.5.3 executable for upgrade.');
    end;
  end;
  if not LockProfile then begin Result := 'Close Harbor Browser before updating it. Its profile is in use.'; Exit; end;
  Result := BackupProfile; if Result <> '' then Exit;
  if not IsDotNetInstalled then begin
    TriedNet := True; Result := Prerequisite('ndp48-x86-x64-allos-enu.exe', '/q /norestart', True);
    if Result <> '' then Exit;
    if not RestartRequired and not IsDotNetInstalled then begin Result := 'The required .NET Framework could not be verified after setup.'; Exit; end;
  end else Log('Existing .NET Framework accepted; helper skipped.');
  if not IsWebViewInstalled then begin
    TriedWeb := True; Result := Prerequisite('MicrosoftEdgeWebView2RuntimeInstallerX64.exe', '/silent /install', False);
    if Result <> '' then Exit;
    if not RestartRequired and not IsWebViewInstalled then begin Result := 'The required WebView2 runtime could not be verified after setup.'; Exit; end;
  end else Log('Existing WebView2 accepted; helper skipped.');
  NeedsRestart := RestartRequired;
end;

function NeedRestart: Boolean;
begin Result := RestartRequired; end;
function CanLaunch: Boolean;
begin Result := not RestartRequired; end;
procedure CurStepChanged(Step: TSetupStep);
begin if Step = ssPostInstall then ReleaseProfile; end;
procedure DeinitializeSetup;
begin ReleaseProfile; end;
function InitializeUninstall: Boolean;
begin
  Result := LockProfile;
  if not Result and not UninstallSilent then MsgBox('Close Harbor Browser before uninstalling it.', mbInformation, MB_OK);
end;
procedure DeinitializeUninstall;
begin ReleaseProfile; end;

