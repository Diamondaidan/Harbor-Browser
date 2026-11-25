[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$app=Get-Item -LiteralPath (Join-Path $root 'HarborBrowser\bin\Release\net48\HarborBrowser.exe')
$suites=@('regression','full','suggestions','recovery','proton','auth-handoff','proton-pending','proton-hotfix2','proton-beta','proton-fallback','proton-guard-handoff','proton-r4','release057','updates','updates-ui','environment061','ui061','proton-off061')
$results=@()
foreach($suite in $suites){
 $path=Join-Path $root ($suite+'-release061.log')
 $present=Test-Path -LiteralPath $path
 $lines=if($present){@(Get-Content -LiteralPath $path)}else{@()}
 $current=$present -and (Get-Item -LiteralPath $path).LastWriteTimeUtc -ge $app.LastWriteTimeUtc
 $passes=@($lines | Where-Object {$_ -match '^PASS '}).Count
 $failed=@($lines | Where-Object {$_ -match '^FAIL '})
 $results+=@{Suite=$suite;Passed=$current -and $failed.Count -eq 0 -and @($lines | Where-Object {$_ -match '^ALL PASSED:'}).Count -gt 0;CurrentExecutable=$current;AssertionsPassed=$passes;Failures=$failed;Log=[IO.Path]::GetFileName($path)}
}
$hash=(Get-FileHash -LiteralPath $app.FullName -Algorithm SHA256).Hash
$integration=Join-Path $root 'IntegrationTests\bin\Release\net48\HarborBrowser.exe'
$regression=Join-Path $root 'Tests\bin\Release\net48\HarborBrowser.exe'
$copiesMatch=(Get-FileHash -LiteralPath $integration -Algorithm SHA256).Hash -eq $hash -and (Get-FileHash -LiteralPath $regression -Algorithm SHA256).Hash -eq $hash
$helper=Join-Path $root 'HarborBrowser\bin\Release\net48\HarborUpdateLauncher.exe'
$helperHash=(Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash
foreach($runner in @('Tests','IntegrationTests','UpdateTests')){
 $copy=Join-Path $root ($runner+'\bin\Release\net48\HarborBrowser.exe')
 $helperCopy=Join-Path $root ($runner+'\bin\Release\net48\HarborUpdateLauncher.exe')
 $copiesMatch=$copiesMatch -and (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -eq $hash -and (Get-FileHash -LiteralPath $helperCopy -Algorithm SHA256).Hash -eq $helperHash
}
$verification=@{
 UpdateLauncherSHA256=$helperHash;Version='0.6.1';RecordedUtc=[DateTime]::UtcNow.ToString('o');AppSHA256=$hash
 ReadyToPackage=$copiesMatch -and @($results | Where-Object {!$_.Passed}).Count -eq 0
 TestCopiesMatch=$copiesMatch;AssertionsPassed=($results | Measure-Object AssertionsPassed -Sum).Sum;Suites=$results
}
$verification | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'RELEASE-VERIFICATION.json') -Encoding utf8
$results | ForEach-Object {[pscustomobject]@{Suite=$_.Suite;Pass=$_.Passed;Current=$_.CurrentExecutable;Assertions=$_.AssertionsPassed;Failures=$_.Failures.Count}} | Format-Table
Write-Output ('Ready to package: '+$verification.ReadyToPackage)
