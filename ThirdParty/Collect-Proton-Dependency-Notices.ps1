[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PnpmCache)
$ErrorActionPreference='Stop'
# Copies exact cached upstream packages into an archive; never edits the cache.
$packages=@{
 '@sentry/browser-utils'='10.58.0'; '@sentry/browser'='10.58.0'; '@sentry/core'='10.58.0'
 '@sentry/feedback'='10.58.0'; '@sentry/replay-canvas'='10.58.0'; '@sentry/replay'='10.58.0'
 'dom-form-serializer'='2.0.0'; 'lz-string'='1.5.0'; 'plural-forms'='0.5.5'
 'throttle-debounce'='5.0.2'; 'uint8array-extras'='1.4.0'; 'webext-detect'='5.3.2'
 'webext-events'='3.1.1'; 'webext-options-sync'='4.3.0'; 'webext-polyfill-kinda'='1.0.2'
 'webextension-polyfill'='0.12.0'
}
$root=Join-Path $PSScriptRoot 'ProtonDependencyNotices'
$inputs=Join-Path $PSScriptRoot 'DependencyInputs'
$archive=Join-Path $PSScriptRoot 'Proton-dependency-inputs.zip'
if((Test-Path -LiteralPath $root) -or (Test-Path -LiteralPath $inputs) -or (Test-Path -LiteralPath $archive)){throw 'Dependency outputs already exist; preserve prior evidence.'}
New-Item -ItemType Directory -Path $root,$inputs | Out-Null
$inventory=@()
foreach($name in ($packages.Keys | Sort-Object)){
 $version=$packages[$name];$encoded=$name.Replace('/','+')+'@'+$version
 $source=Join-Path (Join-Path (Join-Path $PnpmCache $encoded) 'node_modules') $name
 $metadata=Get-Content -LiteralPath (Join-Path $source 'package.json') -Raw | ConvertFrom-Json
 if($metadata.name -ne $name -or $metadata.version -ne $version){throw 'Unexpected cached dependency.'}
 $licenses=Get-ChildItem -LiteralPath $source -File | Where-Object {$_.Name -match '^(licen[sc]e|copying|notice)(\..*)?$'}
 if(!$licenses){throw ('No license found for '+$name)}
 $notices=Join-Path $root $encoded
 New-Item -ItemType Directory -Path $notices | Out-Null
 $licenseInventory=@()
 foreach($license in $licenses){
  Copy-Item -LiteralPath $license.FullName -Destination $notices
  $licenseInventory+=@{File=$license.Name;SHA256=(Get-FileHash -LiteralPath $license.FullName -Algorithm SHA256).Hash}
 }
 $target=Join-Path $inputs $encoded
 & robocopy $source $target /E /XD node_modules /NFL /NDL /NJH /NJS /NP | Out-Null
 if($LASTEXITCODE -ge 8){throw ('Dependency copy failed: '+$name)}
 $inventory+=@{Name=$name;Version=$version;License=$metadata.license;LicenseFiles=$licenseInventory}
}
$inventory | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'inventory.json') -Encoding utf8
& 'C:\Program Files\7-Zip\7z.exe' a -tzip $archive (Join-Path $inputs '*') -mx=7 | Out-Null
if($LASTEXITCODE -ne 0){throw 'Dependency source archive failed.'}
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'source-archive-hash.json') -Encoding utf8
Write-Output ('Collected notices and package inputs for '+$inventory.Count+' pinned dependencies.')
exit 0
