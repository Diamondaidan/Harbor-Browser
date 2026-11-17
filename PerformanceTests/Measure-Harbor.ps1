[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,47}$')]
    [string]$Label,
    [ValidateRange(0,2147483647)]
    [int]$RootProcessId=0,
    [ValidateRange(5,120)]
    [int]$Samples=30,
    [ValidateRange(250,5000)]
    [int]$IntervalMilliseconds=1000,
    [string]$OutputDirectory=(Join-Path $PSScriptRoot 'Results'),
    [string]$ExpectedExecutable=(Join-Path (Split-Path -Parent $PSScriptRoot) 'HarborBrowser-0.5.5-proton-hotfix2\dist\Harbor-Proton-Trial-0.5.5-Hotfix2\HarborBrowser.exe')
)

# Read-only process measurements. This script never opens a browser profile,
# changes routing, closes windows or records command lines, accounts or IPs.
$ErrorActionPreference='Stop'
$expectedPath=[IO.Path]::GetFullPath($ExpectedExecutable)
$outputPath=[IO.Path]::GetFullPath($OutputDirectory)
$allowedOutput=[IO.Path]::GetFullPath($PSScriptRoot)+[IO.Path]::DirectorySeparatorChar
if(!$outputPath.StartsWith($allowedOutput,[StringComparison]::OrdinalIgnoreCase)){
    throw 'Measurement output must remain inside Harbor-Connected-Checks.'
}
if(!(Test-Path -LiteralPath $expectedPath -PathType Leaf)){
    throw 'The expected Hotfix 2 executable was not found.'
}

function Read-Inventory {
    # Explicit property selection excludes CommandLine from the query.
    @(Get-CimInstance -ClassName Win32_Process -Property ProcessId,ParentProcessId,ExecutablePath,CreationDate -ErrorAction Stop)
}

function Creation-Ticks($Record){
    if($null -eq $Record.CreationDate){return 0L}
    ([datetime]$Record.CreationDate).ToUniversalTime().Ticks
}

function Metric-Statistics([object[]]$Values){
    $sorted=@($Values | Where-Object {$null -ne $_} | ForEach-Object {[double]$_} | Sort-Object)
    if($sorted.Count -eq 0){return $null}
    $middle=[int][Math]::Floor($sorted.Count/2)
    if($sorted.Count%2 -eq 0){$median=($sorted[$middle-1]+$sorted[$middle])/2}else{$median=$sorted[$middle]}
    $p95Index=[Math]::Max(0,[int][Math]::Ceiling($sorted.Count*0.95)-1)
    [ordered]@{
        Samples=$sorted.Count
        Median=[Math]::Round($median,3)
        P95=[Math]::Round($sorted[$p95Index],3)
        Minimum=[Math]::Round($sorted[0],3)
        Maximum=[Math]::Round($sorted[-1],3)
    }
}

$initialInventory=Read-Inventory
$rootCandidates=@($initialInventory | Where-Object {
    $_.ExecutablePath -and [string]::Equals([IO.Path]::GetFullPath($_.ExecutablePath),$expectedPath,[StringComparison]::OrdinalIgnoreCase) -and
    ($RootProcessId -eq 0 -or [int]$_.ProcessId -eq $RootProcessId)
})
if($rootCandidates.Count -ne 1){
    throw ('Expected exactly one matching Hotfix 2 process; found '+$rootCandidates.Count+'. Pass -RootProcessId when necessary.')
}
$rootRecord=$rootCandidates[0]
$RootProcessId=[int]$rootRecord.ProcessId
$rootCreation=Creation-Ticks $rootRecord
if($rootCreation -eq 0){throw 'The process creation time could not be verified.'}

$logicalProcessors=[Environment]::ProcessorCount
$rows=New-Object 'System.Collections.Generic.List[object]'
$previousCpu=@{}
$previousElapsed=$null
$previousUtc=$null
$watch=[Diagnostics.Stopwatch]::StartNew()
$startedUtc=[datetime]::UtcNow
$endReason='Requested samples completed'

for($sampleIndex=0;$sampleIndex -lt $Samples;$sampleIndex++){
    $targetSeconds=$sampleIndex*$IntervalMilliseconds/1000.0
    $remainingMilliseconds=[int][Math]::Floor(($targetSeconds-$watch.Elapsed.TotalSeconds)*1000)
    if($remainingMilliseconds -gt 0){Start-Sleep -Milliseconds $remainingMilliseconds}
    try{$inventory=Read-Inventory}catch{
        $endReason='Process inventory could not be read during sampling'
        break
    }
    $currentRoot=@($inventory | Where-Object {[int]$_.ProcessId -eq $RootProcessId -and (Creation-Ticks $_) -eq $rootCreation})
    if($currentRoot.Count -ne 1){
        $endReason='The original Harbor process exited; replacement processes were not followed'
        break
    }

    $recordsById=@{}
    foreach($record in $inventory){$recordsById[[int]$record.ProcessId]=$record}
    $included=New-Object 'System.Collections.Generic.HashSet[int]'
    [void]$included.Add($RootProcessId)
    do{
        $changed=$false
        foreach($record in $inventory){
            $childId=[int]$record.ProcessId
            $parentId=[int]$record.ParentProcessId
            if($included.Contains($childId) -or !$included.Contains($parentId)){continue}
            # Avoid following stale parent PID references after a PID is reused.
            $parentRecord=$recordsById[$parentId]
            if(!$parentRecord -or (Creation-Ticks $record) -lt (Creation-Ticks $parentRecord)){continue}
            if($included.Add($childId)){$changed=$true}
        }
    }while($changed)

    $sampleUtc=[datetime]::UtcNow
    $elapsed=$watch.Elapsed.TotalSeconds
    $currentCpu=@{}
    $privateBytes=0L
    $workingBytes=0L
    $cpuDeltaSeconds=0.0
    $readProcesses=0
    $webViewProcesses=0
    $unreadableProcesses=0
    foreach($processId in $included){
        $process=$null
        try{
            $process=Get-Process -Id $processId -ErrorAction Stop
            $process.Refresh()
            $processStarted=$process.StartTime.ToUniversalTime()
            $recordCreated=Creation-Ticks $recordsById[$processId]
            if([Math]::Abs($processStarted.Ticks-$recordCreated) -gt [TimeSpan]::TicksPerMillisecond*10){
                $unreadableProcesses++
                continue
            }
            $key=[string]$processId+':'+[string]$processStarted.Ticks
            $cpuSeconds=$process.TotalProcessorTime.TotalSeconds
            $currentCpu[$key]=$cpuSeconds
            if($previousCpu.ContainsKey($key)){
                $cpuDeltaSeconds += [Math]::Max(0,$cpuSeconds-$previousCpu[$key])
            }elseif($null -ne $previousUtc -and $processStarted -ge $previousUtc){
                $cpuDeltaSeconds += $cpuSeconds
            }
            $privateBytes += $process.PrivateMemorySize64
            $workingBytes += $process.WorkingSet64
            $readProcesses++
            if($process.ProcessName -ieq 'msedgewebview2'){$webViewProcesses++}
        }catch{$unreadableProcesses++}
        finally{if($null -ne $process){$process.Dispose()}}
    }
    $cpuPercent=$null
    if($null -ne $previousElapsed -and $elapsed -gt $previousElapsed){
        $cpuPercent=[Math]::Round(100*$cpuDeltaSeconds/(($elapsed-$previousElapsed)*$logicalProcessors),3)
    }
    $rows.Add([pscustomobject][ordered]@{
        Sample=$sampleIndex+1
        ElapsedSeconds=[Math]::Round($elapsed,3)
        PrivateCommitMiB=[Math]::Round($privateBytes/1MB,3)
        SummedWorkingSetMiB=[Math]::Round($workingBytes/1MB,3)
        CpuPercentOfLogicalCapacity=$cpuPercent
        InventoriedProcesses=$included.Count
        ReadProcesses=$readProcesses
        WebViewProcesses=$webViewProcesses
        UnreadableProcesses=$unreadableProcesses
    })
    $previousCpu=$currentCpu
    $previousElapsed=$elapsed
    $previousUtc=$sampleUtc
}
$watch.Stop()
$complete=$rows.Count -eq $Samples
if(!$complete -and $endReason -eq 'Requested samples completed'){$endReason='Sampling ended early'}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$fileStem=$Label+'-'+$startedUtc.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6)
$csvPath=Join-Path $outputPath ($fileStem+'.csv')
$jsonPath=Join-Path $outputPath ($fileStem+'.json')
$rows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
$summary=[ordered]@{
    SchemaVersion=1
    Label=$Label
    StartedUtc=$startedUtc.ToString('o')
    RootProcessId=$RootProcessId
    ExecutableFileVersion=(Get-Item -LiteralPath $expectedPath).VersionInfo.FileVersion
    ExecutableSHA256=(Get-FileHash -LiteralPath $expectedPath -Algorithm SHA256).Hash
    Complete=$complete
    EndReason=$endReason
    RequestedSamples=$Samples
    CollectedSamples=$rows.Count
    RequestedIntervalMilliseconds=$IntervalMilliseconds
    ElapsedSeconds=[Math]::Round($watch.Elapsed.TotalSeconds,3)
    LogicalProcessors=$logicalProcessors
    AllProcessesReadable=@($rows | Where-Object {$_.UnreadableProcesses -gt 0}).Count -eq 0
    PrivateCommitMiB=(Metric-Statistics @($rows | ForEach-Object {$_.PrivateCommitMiB}))
    SummedWorkingSetMiB=(Metric-Statistics @($rows | ForEach-Object {$_.SummedWorkingSetMiB}))
    CpuPercentOfLogicalCapacity=(Metric-Statistics @($rows | ForEach-Object {$_.CpuPercentOfLogicalCapacity}))
    ProcessCount=(Metric-Statistics @($rows | ForEach-Object {$_.ReadProcesses}))
    Scope=@(
        'Read-only samples of the verified Harbor process and its currently observed descendants.'
        'The first sample has no CPU interval; CPU summaries exclude that empty value.'
        'CPU is normalized to available logical processors; processes exiting between snapshots can cause CPU undercounting.'
        'Summed working sets can count shared pages more than once. Private commit is not physical RAM usage.'
        'The label describes the user-controlled test phase; this script does not infer VPN state.'
        'Compare phases only with the same build, tabs, workload and sampling duration. No prior benchmark is used.'
    )
    SamplesCsv=[IO.Path]::GetFileName($csvPath)
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
[pscustomobject]@{SummaryPath=$jsonPath;SamplesPath=$csvPath;Complete=$complete;CollectedSamples=$rows.Count} | ConvertTo-Json
if(!$complete){exit 2}
