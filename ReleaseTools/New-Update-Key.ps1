[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PrivateKeyPath)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $PrivateKeyPath){throw 'Preserve the existing release key; refusing to overwrite it.'}
$root=Split-Path $PSScriptRoot
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'updates-v1.public.xml')){throw 'An update trust root already exists. Preserve it for routine releases; key replacement requires a separately planned manual migration.'}
if([IO.Path]::GetFullPath($PrivateKeyPath).StartsWith([IO.Path]::GetFullPath($root)+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Store the private release key outside the application source folder.'}
$directory=Split-Path ([IO.Path]::GetFullPath($PrivateKeyPath))
New-Item -ItemType Directory -Path $directory -Force|Out-Null
Add-Type -AssemblyName System.Security
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider 3072
$rsa.PersistKeyInCsp=$false
try{
 $private=$rsa.ExportCspBlob($true)
 $protected=[Security.Cryptography.ProtectedData]::Protect($private,[Text.Encoding]::UTF8.GetBytes('HarborUpdateManifest/v1'),[Security.Cryptography.DataProtectionScope]::CurrentUser)
 [IO.File]::WriteAllBytes([IO.Path]::GetFullPath($PrivateKeyPath),$protected)
 [Array]::Clear($private,0,$private.Length)
 $public=$rsa.ToXmlString($false)
 [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'updates-v1.public.xml'),$public)
 $code=@'
namespace HarborBrowser.Updates {
 internal static class UpdateTrustRoot {
  internal static UpdateTrust Create(){return new UpdateTrust("Diamondaidan","Harbor-Browser","harbor-updates-v1","PUBLIC_KEY");}
 }
}
'@
 $code=$code.Replace('PUBLIC_KEY',$public.Replace('"','\"'))
 [IO.File]::WriteAllText((Join-Path $root 'HarborBrowser\Updates\UpdateTrustRoot.cs'),$code,[Text.UTF8Encoding]::new($false))
 Write-Output 'Created the public update trust root and a Windows-account-encrypted private release key outside the application source.'
}finally{$rsa.Dispose()}
