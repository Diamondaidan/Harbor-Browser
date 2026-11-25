[CmdletBinding()]
param([Parameter(Mandatory=$true)][string[]]$Files,[Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$CertificateThumbprint,[Parameter(Mandatory=$true)][uri]$TimestampUrl)
$ErrorActionPreference='Stop'
if($TimestampUrl.Scheme -ne 'https' -or $TimestampUrl.UserInfo){throw 'Use your certificate provider''s HTTPS timestamp service.'}
$tools=@(Get-ChildItem -Path (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\*\x64\signtool.exe') -File -ErrorAction SilentlyContinue|Sort-Object FullName -Descending)
if(!$tools.Count){throw 'Install the Windows SDK signing tools first.'}
foreach($path in $Files){
 $file=(Get-Item -LiteralPath $path).FullName
 & $tools[0].FullName sign /fd SHA256 /sha1 $CertificateThumbprint /s My /tr $TimestampUrl.AbsoluteUri /td SHA256 $file
 if($LASTEXITCODE -ne 0){throw 'Signing did not complete.'}
 & $tools[0].FullName verify /pa /all $file
 if($LASTEXITCODE -ne 0){throw 'Windows signature verification failed.'}
 $signature=Get-AuthenticodeSignature -LiteralPath $file
 if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint){throw 'Unexpected or invalid code-signing certificate.'}
}
