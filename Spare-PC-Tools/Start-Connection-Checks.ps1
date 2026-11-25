[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$kit=$PSScriptRoot
$tool=Join-Path $kit 'ConnectionCheck\HarborConnectedChecks.exe'
$manifest=Get-Content -LiteralPath (Join-Path $kit 'kit-manifest.json') -Raw | ConvertFrom-Json
foreach($file in $manifest.Files | Where-Object {$_.Path -like 'ConnectionCheck/*'}){
 if((Get-FileHash -LiteralPath (Join-Path $kit $file.Path) -Algorithm SHA256).Hash -ne $file.SHA256){throw 'A connection-check file changed. Extract a fresh kit.'}
}
$results=Join-Path $kit ('Results\connection-'+[guid]::NewGuid().ToString('N').Substring(0,10))
$process=Start-Process -FilePath $tool -ArgumentList @('"'+$results+'"') -WorkingDirectory (Split-Path $tool) -WindowStyle Hidden -PassThru
try{
 $server=Join-Path $results 'server.json'
 $watch=[Diagnostics.Stopwatch]::StartNew()
 while(!(Test-Path -LiteralPath $server)){
  if($process.HasExited -or $watch.Elapsed.TotalSeconds -gt 15){throw 'The local check could not start. Install the beta prerequisites first.'}
  Start-Sleep -Milliseconds 100
 }
 $address=(Get-Content -LiteralPath $server -Raw | ConvertFrom-Json).url
 Write-Output 'Paste this local address into Harbor Browser Beta, then choose Run checks:'
 Write-Output $address
 Write-Output 'The checks contact ipify and a WebSocket echo service. Results omit public addresses and account data.'
 Write-Output ('Connection results: '+(Join-Path $results 'results.json'))
 [void](Read-Host 'Leave this window open while checking. Press Enter afterward to stop the local server')
}finally{
 $process.Refresh()
 if(!$process.HasExited){
  if(![string]::Equals($process.Path,$tool,[StringComparison]::OrdinalIgnoreCase)){throw 'Local helper identity changed.'}
  Stop-Process -InputObject $process
 }
 $process.Dispose()
}
