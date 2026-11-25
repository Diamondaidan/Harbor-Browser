[CmdletBinding()]
param([switch]$SkipCompile)
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
if(!$SkipCompile){
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $builder=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if(!$builder){throw 'Install Visual Studio Build Tools with .NET desktop development tools.'}
    & $builder (Join-Path $projectRoot 'HarborBrowser\HarborBrowser.csproj') /restore /p:Configuration=Release /v:minimal
    if($LASTEXITCODE -ne 0){throw 'Portable build failed'}
}
$compiled=Join-Path $projectRoot 'HarborBrowser\bin\Release\net48'
if(!(Test-Path -LiteralPath (Join-Path $compiled 'Assets\BrowserVpn\Router\manifest.json'))){throw 'The browser routing extension is missing.'}
$dist=Join-Path $projectRoot 'dist'
$stage=Join-Path $dist ('stage-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -Path (Join-Path $compiled '*') -Destination $stage -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $stage 'READ-ME-FIRST.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'VERIFICATION.md'),(Join-Path $projectRoot 'VPN-PREVIEW-README.md'),(Join-Path $projectRoot 'SETUP-README.md') -Destination $stage
Get-FileHash -LiteralPath (Join-Path $stage 'HarborBrowser.exe'),(Join-Path $stage 'Assets\BrowserVpn\TorRuntime\tor\tor.exe') | ForEach-Object {$_.Hash+'  '+$_.Path.Substring($stage.Length+1)} | Set-Content -LiteralPath (Join-Path $stage 'APP-HASHES.txt')
$licensing=Join-Path $stage 'Licensing'
New-Item -ItemType Directory -Path $licensing -Force | Out-Null
foreach($file in @('tor-0.4.9.16-source.tar.gz','TOR-PROVENANCE.md')){
    Copy-Item -LiteralPath (Join-Path $projectRoot ('ThirdParty\'+$file)) -Destination $licensing
}
$zip=Join-Path $dist 'HarborBrowser-0.5.2-portable.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Get-FileHash -LiteralPath $zip -Algorithm SHA256
Write-Host "Portable release: $zip"

