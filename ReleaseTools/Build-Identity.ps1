function Get-HarborBuildIdentity([string]$Root,[switch]$VerifyCompiled) {
    $version=(Get-Content -LiteralPath (Join-Path $Root 'VERSION.txt') -Raw).Trim()
    $channel=(Get-Content -LiteralPath (Join-Path $Root 'RELEASE-CHANNEL.txt') -Raw).Trim()
    if($version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' -or $channel -notmatch '^(release|rc[1-9][0-9]*)$'){throw 'Invalid Harbor source build identity.'}
    $identity=if($channel -eq 'release'){$version}else{$version+'-rc.'+$channel.Substring(2)}
    if($VerifyCompiled){
        foreach($name in @('HarborBrowser.exe','HarborUpdateLauncher.exe')){
            $file=Get-Item -LiteralPath (Join-Path $Root ('HarborBrowser\bin\Release\net48\'+$name))
            if($file.VersionInfo.FileVersion.Trim() -ne ($version+'.0') -or $file.VersionInfo.ProductVersion.Trim() -ne $identity){throw ('Compiled '+$name+' does not match '+$identity+'. Rebuild both app and launcher before packaging.')}
        }
    }
    [pscustomobject]@{Version=$version;Channel=$channel;InformationalVersion=$identity;DisplayVersion=($identity.Replace('-rc.',' RC'))}
}
