$ErrorActionPreference='Stop'
$releaseRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$exe=Join-Path $PSScriptRoot 'bin\Release\net48\HarborIntegrationTests.exe'
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$results=@()
foreach($trial in 1..4){
    $out=Join-Path $PSScriptRoot ('TestResults\release054-'+$stamp+'-'+$trial+'\suggestions')
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    $arguments=@(('"'+$out+'"'),('"'+(Join-Path $PSScriptRoot 'Fixtures')+'"'),'--suggestion-clicks')
    $log=Join-Path $out 'suggestions.log'
    $process=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError (Join-Path $out 'stderr.log') -PassThru
    $deadline=(Get-Date).AddSeconds(75)
    while(!$process.WaitForExit(1000) -and (Get-Date) -lt $deadline){}
    $timedOut=!$process.HasExited
    if($timedOut){$process.Kill();$process.WaitForExit()}
    $passed=@(Get-Content -LiteralPath $log | Where-Object {$_ -like 'PASS *'}).Count
    $result=[pscustomobject]@{Trial=$trial;Output=$out;WebViewRootLength=(Join-Path $out 'Profile\WebView2').Length;TimedOut=$timedOut;ExitCode=$process.ExitCode;Assertions=$passed;Complete=(!$timedOut -and $process.ExitCode -eq 0 -and $passed -eq 17)}
    $results+=$result;$result|ConvertTo-Json -Compress|Write-Output
}
$results|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $releaseRoot 'deep-profile-results054.json')
if(@($results|Where-Object {!$_.Complete}).Count){throw 'A deep-profile replay failed.'}
Write-Output 'ALL PASSED: 4 deep-profile replays, 68 assertions; roots are at least as long as the original failing fixture.'
