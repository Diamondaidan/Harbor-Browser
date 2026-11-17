[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$releaseRoot=$PSScriptRoot
$checkRoot=Join-Path $releaseRoot 'PerformanceTests'
$integration=Join-Path $releaseRoot 'IntegrationTests\bin\Release\net48\HarborIntegrationTests.exe'
$app=Join-Path $releaseRoot 'HarborBrowser\bin\Release\net48\HarborBrowser.exe'
if((Get-FileHash -LiteralPath $app).Hash -ne (Get-FileHash -LiteralPath (Join-Path (Split-Path $integration) 'HarborBrowser.exe')).Hash){throw 'Build the matching integration test executable first.'}
$root=Join-Path $releaseRoot ('T\R4-resource-'+(Get-Date -Format 'MMddHHmmss'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$phases=@()
foreach($variant in @('legacy','current')){
    $output=Join-Path $root $variant
    New-Item -ItemType Directory -Path $output | Out-Null
    $arguments=@($output,(Join-Path $releaseRoot 'IntegrationTests\Fixtures'),'--proton-r4-benchmark',$variant)
    $process=Start-Process -FilePath $integration -ArgumentList $arguments -WorkingDirectory $releaseRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $output 'output.txt') -RedirectStandardError (Join-Path $output 'error.txt') -PassThru
    try{
        $ready=Join-Path $output 'benchmark-ready.json'
        $wait=[Diagnostics.Stopwatch]::StartNew()
        while(!(Test-Path -LiteralPath $ready)){
            if($process.HasExited){throw ('Owned benchmark exited before readiness: '+$variant)}
            if($wait.Elapsed.TotalSeconds -gt 40){throw ('Owned benchmark readiness timed out: '+$variant)}
            Start-Sleep -Milliseconds 100
        }
        $metadata=Get-Content -LiteralPath $ready -Raw | ConvertFrom-Json
        if($metadata.RootProcessId -ne $process.Id){throw 'Owned benchmark identity mismatch.'}
        $measurement=& (Join-Path $checkRoot 'Measure-Harbor.ps1') -Label ('beta057-r4-'+$variant) -RootProcessId $process.Id -ExpectedExecutable $integration -OutputDirectory (Join-Path $checkRoot 'Results\beta057-r4') -Samples 30
        $paths=($measurement -join [Environment]::NewLine) | ConvertFrom-Json
        if(!$paths.Complete){throw ('Resource sampling did not complete: '+$variant)}
        if(!$process.WaitForExit(20000)){throw ('Owned benchmark did not finish: '+$variant)}
        if($process.ExitCode -ne 0){Get-Content -LiteralPath (Join-Path $output 'output.txt') -Tail 15;throw ('Owned benchmark failed: '+$variant)}
        $native=Get-Content -LiteralPath (Join-Path $output 'route-resource-benchmark.json') -Raw | ConvertFrom-Json
        $summary=Get-Content -LiteralPath $paths.SummaryPath -Raw | ConvertFrom-Json
        $phases+=@{Variant=$variant;Native=$native;Resources=$summary;SummaryFile=$paths.SummaryPath;Output=$output}
        Write-Output ('Completed owned '+$variant+' benchmark.')
    }
    finally{
        $process.Refresh()
        if(!$process.HasExited){
            if(![string]::Equals($process.Path,$integration,[StringComparison]::OrdinalIgnoreCase)){throw 'Owned test process identity changed.'}
            Stop-Process -InputObject $process
        }
        $process.Dispose()
    }
}
$result=@{
 AppSHA256=(Get-FileHash -LiteralPath $app).Hash
 IntegrationSHA256=(Get-FileHash -LiteralPath $integration).Hash
 RecordedUtc=[datetime]::UtcNow.ToString('o')
 Phases=$phases
 Scope='Two owned route-check component scenarios with fake addresses and local endpoints. Legacy reproduces the prior controller/environment lifecycle with the current SDK/parser. This is not a live Proton or full R3-versus-R4 application overhead measurement.'
}
$evidence=Join-Path $releaseRoot 'Evidence\056-r4'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $evidence 'resource-comparison.json') -Encoding utf8
$phases | ForEach-Object {[pscustomobject]@{Variant=$_.Variant;CachedControllers=$_.Native.CachedControllersCreated;ExtraBrowserProcesses=$_.Native.AdditionalDirectBrowserProcesses;PrivateMedianMiB=$_.Resources.PrivateCommitMiB.Median;PrivateP95MiB=$_.Resources.PrivateCommitMiB.P95;CPUP95=$_.Resources.CpuPercentOfLogicalCapacity.P95;AllReadable=$_.Resources.AllProcessesReadable}} | Format-Table
