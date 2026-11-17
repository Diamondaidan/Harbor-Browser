[CmdletBinding()]
param([ValidateSet('regression','full','suggestions','recovery','proton','auth-handoff','proton-pending','proton-hotfix2','proton-beta','proton-fallback','proton-guard-handoff','proton-r4','release057','updates','updates-ui','environment061','ui061','proton-off061')][string[]]$Suites=@('regression','full','suggestions','recovery','proton','auth-handoff','proton-pending','proton-hotfix2','proton-beta','proton-fallback','proton-guard-handoff','proton-r4','release057','updates','updates-ui','environment061','ui061','proton-off061'))
$ErrorActionPreference='Stop'
$releaseRoot=$PSScriptRoot
$output=Join-Path $releaseRoot ('T\'+(Get-Date -Format 'MMddHHmmss'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
function Run-Check([string]$name,[string]$exe,[string[]]$arguments){
    if($Suites -notcontains $name){return}
    Write-Output ('Running '+$name)
    $log=Join-Path $releaseRoot ($name+'-release061.log')
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
Run-Check 'recovery' $integration @((Join-Path $output 'recovery'),$fixtures,'--release053')
Run-Check 'theme' $integration @((Join-Path $output 'theme'),$fixtures,'--theme054')
Run-Check 'tor-monitor' $integration @((Join-Path $output 'tor-monitor'),$fixtures,'--tor054')
Run-Check 'privacy-probes' $integration @((Join-Path $output 'privacy-probes'),$fixtures,'--privacy054')
Run-Check 'proton' $integration @((Join-Path $output 'proton'),$fixtures,'--proton055')
Run-Check 'auth-handoff' $integration @((Join-Path $output 'auth-handoff'),$fixtures,'--auth-handoff')
Run-Check 'proton-pending' $integration @((Join-Path $output 'proton-pending'),$fixtures,'--proton-pending')
Run-Check 'proton-hotfix2' $integration @((Join-Path $output 'proton-hotfix2'),$fixtures,'--proton-hotfix2')
Run-Check 'proton-beta' $integration @((Join-Path $output 'proton-beta'),$fixtures,'--proton-beta')
Run-Check 'proton-fallback' $integration @((Join-Path $output 'proton-fallback'),$fixtures,'--proton-fallback')
Run-Check 'proton-guard-handoff' $integration @((Join-Path $output 'proton-guard-handoff'),$fixtures,'--proton-guard-handoff')
Run-Check 'proton-r4' $integration @((Join-Path $output 'proton-r4'),$fixtures,'--proton-r4')
Run-Check 'release057' $integration @((Join-Path $output 'release057'),$fixtures,'--release057')
Run-Check 'environment061' $integration @((Join-Path $output 'environment061'),$fixtures,'--environment061')
Write-Output ('Browser trial results: '+$output)

Run-Check 'updates' (Join-Path $releaseRoot 'UpdateTests\bin\Release\net48\HarborUpdateTests.exe') @((Join-Path $output 'updates'))
Run-Check 'updates-ui' $integration @((Join-Path $output 'updates-ui'),$fixtures,'--updates060')
Run-Check 'ui061' $integration @((Join-Path $output 'ui061'),$fixtures,'--ui061')
Run-Check 'proton-off061' $integration @((Join-Path $output 'proton-off061'),$fixtures,'--proton-off061')
