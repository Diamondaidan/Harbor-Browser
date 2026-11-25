[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Catalog,[Parameter(Mandatory=$true)][string]$Unpacker)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$workspace=Split-Path -Parent $root
$cases=@(Get-Content -LiteralPath $Catalog -Raw | ConvertFrom-Json | Where-Object {$_.Installer})
$cases+=@([pscustomobject]@{Name='0.7.0-rc1';Tag='local-0.7.0-rc1';Version='0.7.0';Installer=(Join-Path $workspace 'HarborBrowser-0.7.0\dist\HarborBrowserSetup-0.7.0-rc1.exe');Files=@([pscustomobject]@{Name='HarborBrowserSetup-0.7.0-rc1.exe';SHA256='74D5F7EBA8B0B940FD1C022CAC4103632BD68BE10BEE5388915A88A4DF0D12A5'})})
$owned=[IO.Path]::GetFullPath((Join-Path $root 'Evidence\VerifiedLegacy071'))
if(!$owned.StartsWith([IO.Path]::GetFullPath($root)+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Legacy evidence escaped checkout.'}
New-Item -ItemType Directory -Path $owned -Force | Out-Null
$records=foreach($case in $cases){
    $installer=Get-Item -LiteralPath $case.Installer
    $expected=$case.Files | Where-Object {$_.Name -eq $installer.Name}
    if(!$expected -or (Get-FileHash -LiteralPath $installer.FullName).Hash -ne $expected.SHA256){throw ('Pinned original installer differs: '+$case.Name)}
    $output=Join-Path $owned $case.Name
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $app=Join-Path $output 'HarborBrowser.exe'
    $proof=Join-Path $output 'verified.json'
    if(Test-Path -LiteralPath $proof){
        $old=Get-Content -LiteralPath $proof -Raw | ConvertFrom-Json
        if($old.InstallerSHA256 -ne $expected.SHA256 -or (Get-FileHash -LiteralPath $app).Hash -ne $old.AppSHA256){throw 'Cached extraction evidence differs.'}
    }else{
        if(Test-Path -LiteralPath $app){throw 'Unrecorded extraction exists; preserve and investigate.'}
        & $Unpacker -e -b -q ('-d'+$output) $installer.FullName '{app}\HarborBrowser.exe' *> (Join-Path $output 'unpack.log')
        if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $app)){throw ('Original payload extraction failed: '+$case.Name)}
    }
    $file=Get-Item -LiteralPath $app
    if(!$file.VersionInfo.FileVersion.Trim().StartsWith($case.Version+'.') -or $file.VersionInfo.ProductName.Trim() -notin @('Harbor Browser','HarborBrowser')){throw ('Unexpected original application identity: '+$case.Name)}
    $hash=(Get-FileHash -LiteralPath $app).Hash
    $fixture='IntegrationTests\Fixtures\Legacy\HarborBrowser-'+$case.Name+'.exe'
    $target=Join-Path $root $fixture
    if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $target).Hash -ne $hash){throw ('Preserved legacy fixture differs: '+$case.Name)}}else{Copy-Item -LiteralPath $app -Destination $target}
    $record=[pscustomobject]@{Name=$case.Name;AppVersion=$file.VersionInfo.FileVersion.Trim();AppSHA256=$hash;AppBytes=$file.Length;Fixture=$fixture.Replace('\','/');Installer=$installer.Name;InstallerSHA256=$expected.SHA256;ReleaseURL=$(if($case.Tag.StartsWith('v')){'https://github.com/Diamondaidan/Harbor-Browser/releases/tag/'+$case.Tag}else{'Preserved locally validated 0.7.0 RC1'});ExtractedWithoutSetupExecution=$true}
    $record | ConvertTo-Json | Set-Content -LiteralPath $proof -Encoding UTF8
    Write-Host ('VERIFIED '+$case.Name+' '+$hash)
    $record
}
$toolHash=(Get-FileHash -LiteralPath $Unpacker).Hash
[ordered]@{Schema=1;Purpose='Exact application identities recovered from pinned original installer payloads';UnpackerRepository='https://github.com/jrathlev/InnoUnpacker-Windows-GUI';UnpackerSHA256=$toolHash;OriginalInstallersExecuted=$false;Records=$records} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'LegacyReleases.json') -Encoding UTF8
$lines=@('// Generated from LegacyReleases.json. Original installers were inspected, never run.','function IsKnownLegacyHarbor(const AppPath: String): Boolean;','var Hash: String;','begin','  Hash := GetSHA256OfFile(AppPath);','  Result :=')
for($i=0;$i -lt $records.Count;$i++){$last=if($i -eq $records.Count-1){';'}else{' or'};$lines+=('    (CompareText(Hash, '+[char]39+$records[$i].AppSHA256+[char]39+') = 0)'+$last)}
$lines+=@('end;','')
Set-Content -LiteralPath (Join-Path $PSScriptRoot 'LegacyReleaseHashes.iss') -Value $lines -Encoding ASCII
Write-Output ('ALL VERIFIED: '+$records.Count+' original application identities. No historical installer or browser was executed.')
