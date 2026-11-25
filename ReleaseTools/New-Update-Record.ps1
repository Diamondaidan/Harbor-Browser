[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$Installer,
 [Parameter(Mandatory=$true)][string]$PrivateKeyPath,
 [Parameter(Mandatory=$true)][ValidateRange(1,9223372036854775807)][long]$Sequence,
 [Parameter(Mandatory=$true)][string]$OutputPath,
 [string]$PreviousInstaller,
 [string]$NotesPath=(Join-Path (Split-Path $PSScriptRoot) 'RELEASE-NOTES.md'),
 [ValidateRange(1,30)][int]$ValidDays=14,
 [switch]$AllowUnsigned
)
$ErrorActionPreference='Stop'
function Package([string]$Path){
 $file=Get-Item -LiteralPath $Path
 $version=$file.VersionInfo.FileVersion.Trim()
 if($version -notmatch '^(\d+\.\d+\.\d+)\.0$'){throw 'Installer file version is invalid.'}
 $number=$Matches[1]
 if($file.VersionInfo.ProductName.Trim() -ne 'Harbor Browser'){throw 'This is not a Harbor Browser installer.'}
 $signature=Get-AuthenticodeSignature -LiteralPath $file.FullName
 $certificateHash=$null
 if($signature.Status -eq 'Valid'){
  $sha=[Security.Cryptography.SHA256]::Create()
  try{$certificateHash=[BitConverter]::ToString($sha.ComputeHash($signature.SignerCertificate.RawData)).Replace('-','')}finally{$sha.Dispose()}
 }elseif(!$AllowUnsigned -or $signature.Status -ne 'NotSigned'){throw 'The installer is not validly signed. Use -AllowUnsigned only for an intentionally unsigned local release.'}
 [ordered]@{Version=$number;Url=('https://github.com/Diamondaidan/Harbor-Browser/releases/download/v'+$number+'/HarborBrowserSetup-'+$number+'-release.exe');Sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash;Bytes=$file.Length;DataSchema=9;CertificateSha256=$certificateHash}
}
if(Test-Path -LiteralPath $OutputPath){throw 'Preserve existing signed records; choose a new output path.'}
$latest=Package $Installer
$previous=if($PreviousInstaller){Package $PreviousInstaller}else{$null}
if($previous -and [version]$previous.Version -ge [version]$latest.Version){throw 'The previous installer must be older.'}
$now=[datetime]::UtcNow
$notes=[IO.File]::ReadAllText([IO.Path]::GetFullPath($NotesPath))
if($notes.Length -gt 12000){throw 'Release notes exceed the updater limit.'}
$payload=[ordered]@{Schema=1;Product='HarborBrowser';Channel='stable';Architecture='x64';Sequence=$Sequence;IssuedUtc=$now.ToString('o');ExpiresUtc=$now.AddDays($ValidDays).ToString('o');Notes=$notes;Latest=$latest;Previous=$previous;RollbackFrom=if($previous){$latest.Version}else{$null}}
$bytes=[Text.Encoding]::UTF8.GetBytes(($payload|ConvertTo-Json -Depth 6 -Compress))
Add-Type -AssemblyName System.Security
$secret=$null;$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
try{
 $secret=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($PrivateKeyPath)),[Text.Encoding]::UTF8.GetBytes('HarborUpdateManifest/v1'),[Security.Cryptography.DataProtectionScope]::CurrentUser)
 $rsa.ImportCspBlob($secret)
 if($rsa.ToXmlString($false) -ne [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'updates-v1.public.xml'))){throw 'Private release key does not match the public update root.'}
 $signature=$rsa.SignData($bytes,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
 $envelope=[ordered]@{KeyId='harbor-updates-v1';Payload=[Convert]::ToBase64String($bytes);Signature=[Convert]::ToBase64String($signature)}
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),($envelope|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
 Write-Output ('Prepared signed release record for '+$latest.Version+'. No GitHub upload was performed.')
}finally{if($secret){[Array]::Clear($secret,0,$secret.Length)};$rsa.Dispose()}
