[CmdletBinding()]
param([string]$Executable,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if(!$Executable){$Executable=Join-Path $PSScriptRoot 'bin\Release\net48\HarborConnectedChecks.exe'}
$tool=[IO.Path]::GetFullPath($Executable)
if(!$OutputDirectory){$OutputDirectory=Join-Path $root ('T\connection-helper-'+[guid]::NewGuid().ToString('N'))}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(!$tool.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)-or !$output.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Use owned paths inside this checkout.'}
if(Test-Path -LiteralPath $output){throw 'Use a new evidence directory.'}
New-Item -ItemType Directory -Path $output | Out-Null
$server=Join-Path $output 'server'
$process=Start-Process -FilePath $tool -ArgumentList @('"'+$server+'"') -WorkingDirectory (Split-Path -Parent $tool) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $output 'stdout.txt') -RedirectStandardError (Join-Path $output 'stderr.txt') -PassThru
Add-Type -AssemblyName System.Net.Http
$handler=[Net.Http.HttpClientHandler]::new();$handler.UseProxy=$false;$handler.UseCookies=$false;$handler.AllowAutoRedirect=$false
$client=[Net.Http.HttpClient]::new($handler);$client.Timeout=[TimeSpan]::FromSeconds(5)
$checks=[Collections.Generic.List[object]]::new()
try{
 $watch=[Diagnostics.Stopwatch]::StartNew()
 while(!(Test-Path -LiteralPath (Join-Path $server 'server.json'))){if($process.HasExited-or $watch.Elapsed.TotalSeconds -gt 15){throw 'Local helper did not start.'};Start-Sleep -Milliseconds 100}
 $metadata=Get-Content -LiteralPath (Join-Path $server 'server.json') -Raw | ConvertFrom-Json
 $base=[uri]$metadata.url
 if($base.Host -ne '127.0.0.1'-or $base.Scheme -ne 'http'-or $metadata.processId -ne $process.Id){throw 'Unexpected helper endpoint or process.'}
 $cases=@(
  @{Name='page';Target='';Code=200},@{Name='script';Target='app.js';Code=200},
  @{Name='worker';Target='worker.js';Code=200},@{Name='service-worker';Target='sw.js';Code=200},
  @{Name='empty-status';Target='status';Code=200},@{Name='wrong-capability';Target='/not-the-capability/';Code=404},
  @{Name='wrong-host';Target='';Code=403;Host='foreign.invalid'},
  @{Name='wrong-origin';Target='api/finish';Code=403;Post=$true}
 )
 foreach($case in $cases){
  $target=[uri]::new($base,[string]$case.Target)
  $method=if($case.Post){[Net.Http.HttpMethod]::Post}else{[Net.Http.HttpMethod]::Get}
  $request=[Net.Http.HttpRequestMessage]::new($method,$target)
  if($case.Host){$request.Headers.Host=$case.Host}
  if($case.Post){[void]$request.Headers.TryAddWithoutValidation('Origin','https://foreign.invalid');$request.Content=[Net.Http.StringContent]::new('{}',[Text.Encoding]::UTF8,'application/json')}
  $response=$null
  try{
   $response=$client.SendAsync($request).GetAwaiter().GetResult()
   $body=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
   if([int]$response.StatusCode -ne $case.Code){throw ('Unexpected HTTP result: '+$case.Name)}
   if($case.Name -eq 'empty-status' -and $body.Trim() -ne '[]'){throw 'A new helper unexpectedly contains route results.'}
   $checks.Add([pscustomobject]@{Name=$case.Name;Status=[int]$response.StatusCode;Passed=$true})
  }finally{if($response){$response.Dispose()};$request.Dispose()}
 }
 $saved=Get-Content -LiteralPath (Join-Path $server 'results.json') -Raw
 if($saved.Trim() -ne '[]'){throw 'Local-only checks unexpectedly created route data.'}
 $result=[pscustomobject]@{Passed=$true;Checks=$checks;ExternalServicesContacted=$false;AccountAccess=$false;Scope='HTTP loopback asset delivery and request rejection only. No route checks, JavaScript execution, VPN control or browser sign-in.'}
 $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'verification.json') -Encoding utf8
 Write-Output ('ALL PASSED: '+$checks.Count+' local connection-helper checks; no external services contacted.')
}finally{
 $client.Dispose();$handler.Dispose();$process.Refresh()
 if(!$process.HasExited){if(![string]::Equals($process.Path,$tool,[StringComparison]::OrdinalIgnoreCase)){throw 'Owned helper identity changed.'};Stop-Process -InputObject $process}
 $process.Dispose()
}
