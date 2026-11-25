[CmdletBinding()]
param([switch]$SkipBuild,[switch]$Latency,[ValidateSet('direct','tor','proton-signed-out')][string[]]$Modes=@('direct','tor','proton-signed-out'),[ValidateRange(1,3)][int]$Trials=3)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($PSScriptRoot)
$release=Split-Path -Parent $root
if(!$SkipBuild){
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $builder=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if(!$builder){throw 'Visual Studio desktop build tools are required.'}
    & $builder (Join-Path $root 'ConnectionBenchmarks.csproj') /restore /p:Configuration=Release /v:minimal
    if($LASTEXITCODE -ne 0){throw 'Connection benchmark build failed.'}
}
$exe=Join-Path $root 'bin\Release\net48\HarborIntegrationTests.exe'
$output=Join-Path $root ('TestResults\comparison-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$results=@()
# Rotate order across trials rather than always measuring a provider last.
$orders=@(@('direct','tor','proton-signed-out'),@('proton-signed-out','direct','tor'),@('tor','proton-signed-out','direct'))
foreach($trial in 1..$Trials){
    foreach($mode in $orders[$trial-1]){
        if($Modes -notcontains $mode){continue}
        $case=Join-Path $output ($mode+'-trial'+$trial)
        $arguments=@($case,$mode,[string]$trial)
        if($Latency){$arguments+='--latency'}
        Write-Output ('Running '+$mode+' trial '+$trial)
        & $exe @arguments *> ($case+'.log')
        $code=$LASTEXITCODE
        Get-Content -LiteralPath ($case+'.log') -Tail 2
        if(!(Test-Path -LiteralPath (Join-Path $case 'measurements.json'))){throw ('No case evidence: '+$mode+' '+$trial)}
        $result=Get-Content -LiteralPath (Join-Path $case 'measurements.json') -Raw | ConvertFrom-Json
        $results+=$result
        if($code -ne 0 -or !$result.Passed -or !$result.CleanupPassed -or !$result.WindowsProxyUnchanged){throw ('Connection case failed: '+$mode+' '+$trial)}
    }
}
$samples=foreach($result in $results){foreach($sample in $result.Samples){[pscustomobject]@{Mode=$result.Mode;Trial=$result.Trial;Phase=$sample.Phase;Index=$sample.Index;Processes=$sample.Processes;WebViewProcesses=$sample.WebViewProcesses;TorProcesses=$sample.TorProcesses;CpuPercent=$sample.CpuPercent;HostWorkingMB=$sample.HostWorkingMB;HostPrivateMB=$sample.HostPrivateMB;WebViewWorkingMB=$sample.WebViewWorkingMB;WebViewPrivateMB=$sample.WebViewPrivateMB;TorWorkingMB=$sample.TorWorkingMB;TorPrivateMB=$sample.TorPrivateMB;TotalWorkingMB=$sample.TotalWorkingMB;TotalPrivateMB=$sample.TotalPrivateMB}}}
$samples | Export-Csv -LiteralPath (Join-Path $output 'samples.csv') -NoTypeInformation
$summary=$samples | Group-Object Mode,Phase | ForEach-Object { $sample=$_.Group[0];[pscustomobject]@{Mode=$sample.Mode;Phase=$sample.Phase;Samples=$_.Count;CpuPercent=($_.Group|Measure-Object CpuPercent -Average).Average;TotalWorkingMB=($_.Group|Measure-Object TotalWorkingMB -Average).Average;TotalPrivateMB=($_.Group|Measure-Object TotalPrivateMB -Average).Average;TorWorkingMB=($_.Group|Measure-Object TorWorkingMB -Average).Average;TorPrivateMB=($_.Group|Measure-Object TorPrivateMB -Average).Average}}
$summary | Export-Csv -LiteralPath (Join-Path $output 'summary.csv') -NoTypeInformation
$startup=$results | ForEach-Object {[pscustomobject]@{Mode=$_.Mode;Trial=$_.Trial;EnvironmentStartupMs=$_.EnvironmentStartupMs;ProviderSetupMs=$_.ProviderSetupMs;WorkloadStartupMs=$_.WorkloadStartupMs;SuccessfulExternalNavigations=$_.ExternalNavigationMs.Count;ExternalNavigationMeanMs=if($_.ExternalNavigationMs.Count){($_.ExternalNavigationMs|Measure-Object -Average).Average}else{$null};ExternalNavigationErrors=$_.ExternalNavigationErrors.Count;ProviderControlsEvidence=$_.ProviderControlsEvidence}}
$startup | Export-Csv -LiteralPath (Join-Path $output 'startup-and-navigation.csv') -NoTypeInformation
@{CompletedCases=$results.Count;Samples=@($samples).Count;Trials=$Trials;Modes=$Modes;Passed=$true;Scope='Standalone five-document WebView2 host. Signed-out Proton is NOT connected. Includes owned Tor helper, host and all environment process IDs. Local workload does not measure encrypted network throughput or battery.'} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'comparison.json')
Write-Output ('Results: '+$output)
$summary | Format-Table Mode,Phase,Samples,CpuPercent,TotalWorkingMB,TotalPrivateMB,TorWorkingMB,TorPrivateMB
