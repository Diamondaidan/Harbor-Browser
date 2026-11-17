[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $root ('T\Signing-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(!$output.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Use an owned output directory inside the source checkout.'}
if(Test-Path -LiteralPath $output){throw 'Use a new output directory; prior candidates must be preserved.'}
New-Item -ItemType Directory -Path $output | Out-Null
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$builder=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if(!$builder){throw 'Visual Studio desktop build tools and .NET Framework 4.8 targeting tools are required.'}
foreach($project in @('UpdateTests\HarborUpdateTests.csproj','Tests\HarborRegressionTests.csproj','IntegrationTests\HarborIntegrationTests.csproj')){
 $log=Join-Path $output ([IO.Path]::GetFileNameWithoutExtension($project)+'-build.log')
 & $builder (Join-Path $root $project) /restore /p:Configuration=Release /v:minimal /nologo *> $log
 if($LASTEXITCODE -ne 0){Get-Content -LiteralPath $log -Tail 30;throw ('Build failed: '+$project)}
}
$compiled=Join-Path $root 'HarborBrowser\bin\Release\net48'
$app=Join-Path $compiled 'HarborBrowser.exe'
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION.txt') -Raw).Trim()
$metadata=@()
foreach($name in @('HarborBrowser.exe','HarborUpdateLauncher.exe')){
 $file=Get-Item -LiteralPath (Join-Path $compiled $name)
 if($file.VersionInfo.ProductName -ne 'Harbor Browser' -or $file.VersionInfo.ProductVersion -ne $version -or $file.VersionInfo.FileVersion -ne ($version+'.0')){throw ('Signing metadata mismatch: '+$name)}
 $metadata+=[pscustomobject]@{Name=$name;Product=$file.VersionInfo.ProductName;ProductVersion=$file.VersionInfo.ProductVersion;FileVersion=$file.VersionInfo.FileVersion;SHA256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
}
$provider=Join-Path $root 'HarborBrowser\Assets\BrowserVpn\Proton'
$providerFiles=@(Get-Content -LiteralPath (Join-Path $root 'ThirdParty\PROTON-PACKAGE-HASHES.json') -Raw | ConvertFrom-Json)
foreach($item in $providerFiles){if((Get-FileHash -LiteralPath (Join-Path $provider $item.Path) -Algorithm SHA256).Hash -ne $item.SHA256){throw ('Upstream provider changed: '+$item.Path)}}
$updateLog=Join-Path $output 'updates.log'
& (Join-Path $root 'UpdateTests\bin\Release\net48\HarborUpdateTests.exe') (Join-Path $output 'update-fixtures') *> $updateLog
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath $updateLog -Tail 30;throw 'Update checks failed.'}
$regressionLog=Join-Path $output 'regression.log'
& (Join-Path $root 'Tests\bin\Release\net48\HarborRegressionTests.exe') (Join-Path $output 'regression-fixtures') (Join-Path $root 'Tests\Fixtures\HarborBrowser-0.3.1-build-fix.zip') $app (Join-Path $compiled 'Microsoft.Web.WebView2.Core.dll') (Join-Path $root 'HarborBrowser\MainWindow.xaml') *> $regressionLog
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath $regressionLog -Tail 30;throw 'Regression checks failed.'}
$signingInput=Join-Path $output 'signing-input'
New-Item -ItemType Directory -Path $signingInput | Out-Null
foreach($item in $metadata){Copy-Item -LiteralPath (Join-Path $compiled $item.Name) -Destination $signingInput}
$summary=[pscustomobject]@{Version=$version;Product='Harbor Browser';Files=$metadata;ProviderFilesVerified=$providerFiles.Count;UpdateAssertions=@(Get-Content -LiteralPath $updateLog | Where-Object {$_ -match '^PASS '}).Count;RegressionAssertions=@(Get-Content -LiteralPath $regressionLog | Where-Object {$_ -match '^PASS '}).Count;Status='Unsigned signing input only; not an approved release';FullBrowserAndInstallerValidation='Required after signing and packaging'}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'candidate-summary.json') -Encoding utf8
Write-Output ($summary | ConvertTo-Json -Depth 6)
