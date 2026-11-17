[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$kit=$PSScriptRoot
$manifest=Get-Content -LiteralPath (Join-Path $kit 'kit-manifest.json') -Raw | ConvertFrom-Json
$files=@()
foreach($entry in $manifest.Files){
 $path=[IO.Path]::GetFullPath((Join-Path $kit $entry.Path))
 if(!$path.StartsWith([IO.Path]::GetFullPath($kit)+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected kit path.'}
 $ok=(Test-Path -LiteralPath $path -PathType Leaf) -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $entry.SHA256
 $files+=[pscustomobject]@{Name=$entry.Path;Matches=$ok}
}
$release=0
try{$release=[int](Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release).Release}catch{}
$versions=@()
$client='SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
foreach($hive in @([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryHive]::CurrentUser)){
 foreach($view in @([Microsoft.Win32.RegistryView]::Registry32,[Microsoft.Win32.RegistryView]::Registry64)){
  $base=$null;$key=$null
  try{$base=[Microsoft.Win32.RegistryKey]::OpenBaseKey($hive,$view);$key=$base.OpenSubKey($client);if($key){$value=[string]$key.GetValue('pv');if($value){$versions+=$value}}}finally{if($key){$key.Dispose()};if($base){$base.Dispose()}}
 }
}
$build=[int](Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name CurrentBuildNumber).CurrentBuildNumber
$architecture=if($env:PROCESSOR_ARCHITEW6432){$env:PROCESSOR_ARCHITEW6432}else{$env:PROCESSOR_ARCHITECTURE}
$result=[pscustomobject]@{
 RecordedUtc=[datetime]::UtcNow.ToString('o')
 Version=$manifest.Version
 WindowsBuild=$build
 Is64BitOS=[Environment]::Is64BitOperatingSystem
 Architecture=$architecture
 MeetsPlatform=($architecture -eq 'AMD64' -and $build -ge 19041)
 DotNet48Present=($release -ge 528040)
 WebView2Versions=@($versions | Sort-Object -Unique)
 WebView157Present=@($versions | Where-Object {$_ -match '^(\d+)\.' -and [int]$Matches[1] -ge 157}).Count -gt 0
 Files=$files
 PackageFilesMatch=@($files | Where-Object {!$_.Matches}).Count -eq 0
 HarborProfileAlreadyExists=(Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'HarborBrowser'))
 Scope='Read-only environment and kit integrity check. No installer, profile, account, VPN or Windows setting was changed. Prerequisites can be provided by the included installer.'
}
$output=Join-Path $kit 'Results'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$path=Join-Path $output ('PC-check-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.json')
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $path -Encoding utf8
Write-Output ('Package files match: '+$result.PackageFilesMatch)
Write-Output ('Supported Windows build/architecture: '+$result.MeetsPlatform)
Write-Output ('Existing .NET / WebView2: '+$result.DotNet48Present+' / '+$result.WebView157Present)
Write-Output ('Results saved: '+$path)
if(!$result.PackageFilesMatch -or !$result.MeetsPlatform){exit 1}
exit 0
