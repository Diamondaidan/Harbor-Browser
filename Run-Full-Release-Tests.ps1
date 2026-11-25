[CmdletBinding()]
param([ValidateSet('regression','full','suggestions','home','tor','hardening','live-sites')][string[]]$Suites=@('regression','full','suggestions','home','tor','hardening','live-sites'))
$ErrorActionPreference='Stop'
$releaseRoot=$PSScriptRoot
$output=Join-Path $releaseRoot ('T\'+(Get-Date -Format 'MMddHHmmss'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
function Run-Check([string]$name,[string]$exe,[string[]]$arguments){
    if($Suites -notcontains $name){return}
    Write-Output ('Running '+$name)
    $log=Join-Path $releaseRoot ($name+'-release052.log')
    & $exe @arguments *> $log
    if($LASTEXITCODE -ne 0){Get-Content -LiteralPath $log -Tail 25;throw ($name+' failed: '+$LASTEXITCODE)}
    Get-Content -LiteralPath $log -Tail 2
}
$app=Join-Path $releaseRoot 'HarborBrowser\bin\Release\net48\HarborBrowser.exe'
Run-Check 'regression' (Join-Path $releaseRoot 'Tests\bin\Release\net48\HarborRegressionTests.exe') @((Join-Path $output 'regression'),(Join-Path $releaseRoot 'Tests\Fixtures\HarborBrowser-0.3.1-build-fix.zip'),$app,(Join-Path (Split-Path $app) 'Microsoft.Web.WebView2.Core.dll'),(Join-Path $releaseRoot 'HarborBrowser\MainWindow.xaml'))
$integration=Join-Path $releaseRoot 'IntegrationTests\bin\Release\net48\HarborIntegrationTests.exe'
$fixtures=Join-Path $releaseRoot 'IntegrationTests\Fixtures'
Run-Check 'full' $integration @((Join-Path $output 'full'),$fixtures)
Run-Check 'suggestions' $integration @((Join-Path $output 'suggestions'),$fixtures,'--suggestion-clicks')
Run-Check 'home' (Join-Path $releaseRoot 'HomeSmokeTests\bin\Release\net48\HarborHomeSmoke.exe') @((Join-Path $releaseRoot 'dist\payload\Assets\Home'),(Join-Path $output 'home'))
Run-Check 'tor' $integration @((Join-Path $output 'tor'),$fixtures,'--browser-vpn')
Run-Check 'hardening' $integration @((Join-Path $output 'hardening'),$fixtures,'--release052')
Run-Check 'live-sites' $integration @((Join-Path $output 'live-sites'),$fixtures,'--live-sites')
Write-Output ('Browser release results: '+$output)
