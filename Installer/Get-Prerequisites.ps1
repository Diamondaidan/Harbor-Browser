[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$folder=Join-Path $PSScriptRoot 'redist'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$urls=@{
 'ndp48-x86-x64-allos-enu.exe'='https://go.microsoft.com/fwlink/?linkid=2088631'
 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe'='https://go.microsoft.com/fwlink/p/?LinkId=2124701'
}
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'redist-manifest.json') -Raw | ConvertFrom-Json
foreach($item in $manifest){
 $file=Join-Path $folder $item.Name
 if(!(Test-Path -LiteralPath $file)){
  $partial=$file+'.download'
  if(Test-Path -LiteralPath $partial){throw 'A previous download is present. Review it before retrying.'}
  Invoke-WebRequest -Uri $urls[$item.Name] -OutFile $partial -UseBasicParsing -MaximumRedirection 10
  if((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $item.SHA256){throw 'The Microsoft package changed. Review and update the tested prerequisite manifest; do not disable verification.'}
  Move-Item -LiteralPath $partial -Destination $file
 }
 $signature=Get-AuthenticodeSignature -LiteralPath $file
 if((Get-Item $file).Length -ne $item.Bytes -or (Get-FileHash $file -Algorithm SHA256).Hash -ne $item.SHA256 -or $signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation'){throw ('Unverified prerequisite: '+$item.Name)}
 Write-Output ('Verified '+$item.Name)
}
