[CmdletBinding()]
param([switch]$Offline)
$ErrorActionPreference='Stop'
$trialRoot=[IO.Path]::GetFullPath((Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$revision='71d7d86f164a2ee8e50ce5a389c9e5cbe0fd6883'
$archive=Join-Path $trialRoot 'ThirdParty\ProtonVPN-source.zip'
if((Get-FileHash -LiteralPath $archive).Hash -ne '975BF0C429BA3EA030871E5719913ABD13D6DE1E2BBEE2D3D936B465BB49F0D0'){throw 'Proton source archive hash differs from the reviewed download.'}
$work=Join-Path $PSScriptRoot ('Work-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force|Out-Null
Expand-Archive -LiteralPath $archive -DestinationPath $work
$source=Join-Path $work ('proton-vpn-browser-extension-'+$revision)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'pnpm-lock.yaml') -Destination (Join-Path $source 'pnpm-lock.yaml')
$installArgs=@('install','--ignore-scripts','--frozen-lockfile','--store-dir',(Join-Path $PSScriptRoot 'store'))
if($Offline){$installArgs+='--offline'}
Push-Location $source
try{
    & pnpm @installArgs
    if($LASTEXITCODE -ne 0){throw 'Pinned Proton dependency restore failed.'}
    & pnpm exec webpack --mode=production
    if($LASTEXITCODE -ne 0){throw 'Official Proton production build failed.'}
}finally{Pop-Location}
$distribution=Join-Path $source 'distribution'
$package=Join-Path $trialRoot 'HarborBrowser\Assets\BrowserVpn\Proton'
if(![IO.Path]::GetFullPath($package).StartsWith($trialRoot+'\HarborBrowser\Assets\BrowserVpn\',[StringComparison]::OrdinalIgnoreCase)){throw 'Proton package escaped the trial assets directory.'}
$manifest=Get-Content -LiteralPath (Join-Path $distribution 'manifest.json') -Raw|ConvertFrom-Json
if($manifest.version -ne '1.3.6' -or !$manifest.key){throw 'Unexpected Proton build identity.'}
foreach($relative in @('manifest.json','popup.html','js\background.js','js\popup.js','js\service.js','js\browser-polyfill.min.js')){
    if(!(Test-Path -LiteralPath (Join-Path $distribution $relative)) -or (Get-Item -LiteralPath (Join-Path $distribution $relative)).Length -eq 0){throw ('Missing or empty Proton output: '+$relative)}
}
New-Item -ItemType Directory -Path $package -Force|Out-Null
Get-ChildItem -LiteralPath $distribution -Force|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $package -Recurse -Force}
Copy-Item -LiteralPath (Join-Path $source 'LICENSE') -Destination $package -Force
Write-Output ('Built unmodified Proton extension '+$manifest.version+' at '+$package)
Write-Output 'No extension was installed into any browser/profile by this build script.'
