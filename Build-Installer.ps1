[CmdletBinding()]
param(
    [string]$Version = '0.4.3',
    [ValidateRange(0,65535)][int]$BuildRevision = 0,
    [string]$ReleaseSuffix = '',
    [switch]$SkipPrerequisiteDownloads,
    [switch]$SkipCompile
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Solution = Join-Path $ProjectRoot 'HarborBrowser.sln'
$ProjectDir = Join-Path $ProjectRoot 'HarborBrowser'
$BuildOutputCandidates = @((Join-Path $ProjectDir 'bin\Release\net48'), (Join-Path $ProjectDir 'bin\Release'))
$BuildOutput = $null
$PayloadDir = Join-Path $ProjectRoot 'dist\payload'
$RedistDir = Join-Path $ProjectRoot 'Installer\redist'
$IssFile = Join-Path $ProjectRoot 'Installer\HarborBrowser.iss'
$DotNetInstaller = Join-Path $RedistDir 'ndp48-x86-x64-allos-enu.exe'
$WebViewInstaller = Join-Path $RedistDir 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe'

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $matches = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null
        $candidate = $matches | Select-Object -First 1
        if ($candidate -and (Test-Path $candidate)) { return $candidate }
    }

    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $known = @(
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'),
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe'),
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'),
        (Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe')
    )
    foreach ($path in $known) { if (Test-Path $path) { return $path } }
    return $null
}

function Format-Bytes([long]$Bytes) {
    if ($Bytes -ge 1GB) { return ('{0:N2} GB' -f ($Bytes / 1GB)) }
    if ($Bytes -ge 1MB) { return ('{0:N1} MB' -f ($Bytes / 1MB)) }
    if ($Bytes -ge 1KB) { return ('{0:N1} KB' -f ($Bytes / 1KB)) }
    return "$Bytes bytes"
}

function Test-MicrosoftSignature([string]$Path, [string]$ExpectedPublisher) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    $signature = Get-AuthenticodeSignature -FilePath $Path
    return ($signature.Status -eq 'Valid' -and
        $signature.SignerCertificate -and
        $signature.SignerCertificate.Subject -match $ExpectedPublisher)
}

function Download-AndVerify(
    [string]$Url,
    [string]$Destination,
    [string]$ExpectedPublisher,
    [long]$MinimumBytes = 1MB
) {
    $leaf = Split-Path $Destination -Leaf
    $minimumText = Format-Bytes $MinimumBytes

    if (Test-Path -LiteralPath $Destination -PathType Leaf) {
        $existingLength = (Get-Item -LiteralPath $Destination).Length
        if ($existingLength -ge $MinimumBytes -and (Test-MicrosoftSignature $Destination $ExpectedPublisher)) {
            Write-Host "Already downloaded, size-checked, and signature verified: $leaf ($(Format-Bytes $existingLength))" -ForegroundColor Green
            return
        }
        Write-Warning "Discarding an existing $leaf because it is too small or its Microsoft signature is invalid."
        Remove-Item -LiteralPath $Destination -Force
    }

    $partial = "$Destination.partial"
    $attemptErrors = New-Object System.Collections.Generic.List[string]
    $methods = @()
    if (Get-Command Start-BitsTransfer -ErrorAction SilentlyContinue) {
        $methods += 'BITS'
    }
    $methods += 'Invoke-WebRequest'

    foreach ($method in $methods) {
        if (Test-Path -LiteralPath $partial) {
            Remove-Item -LiteralPath $partial -Force
        }

        try {
            Write-Host "Downloading $leaf using $method..."
            if ($method -eq 'BITS') {
                Start-BitsTransfer -Source $Url -Destination $partial -TransferType Download -DisplayName "Harbor Browser prerequisite: $leaf" -ErrorAction Stop
            }
            else {
                Invoke-WebRequest -Uri $Url -OutFile $partial -UseBasicParsing -TimeoutSec 1800 -MaximumRedirection 10 -ErrorAction Stop
            }

            if (!(Test-Path -LiteralPath $partial -PathType Leaf)) {
                throw 'The download command completed without creating a file.'
            }

            $length = (Get-Item -LiteralPath $partial).Length
            if ($length -lt $MinimumBytes) {
                $sig = Get-AuthenticodeSignature -FilePath $partial
                $signer = if ($sig.SignerCertificate) { $sig.SignerCertificate.Subject } else { 'No signer information' }
                throw "Downloaded only $(Format-Bytes $length); the expected package must be at least $minimumText. Signature status: $($sig.Status). Signer: $signer. This can indicate a blocked/partial response or a small bootstrapper instead of the full offline installer."
            }

            $signature = Get-AuthenticodeSignature -FilePath $partial
            if ($signature.Status -ne 'Valid') {
                throw "Authenticode validation failed (status: $($signature.Status)). The file will not be packaged."
            }
            if (!$signature.SignerCertificate -or $signature.SignerCertificate.Subject -notmatch $ExpectedPublisher) {
                $actualSigner = if ($signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { 'unknown' }
                throw "Unexpected signer '$actualSigner'. Expected publisher matching '$ExpectedPublisher'."
            }

            Move-Item -LiteralPath $partial -Destination $Destination -Force
            Write-Host "Microsoft signature verified: $leaf ($(Format-Bytes $length))" -ForegroundColor Green
            return
        }
        catch {
            $attemptErrors.Add("$method`: $($_.Exception.Message)")
            Write-Warning "$method download attempt did not produce a valid package: $($_.Exception.Message)"
            if (Test-Path -LiteralPath $partial) {
                Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
            }
        }
    }

    $details = $attemptErrors -join [Environment]::NewLine
    throw @"
Could not obtain a complete, Microsoft-signed $leaf.
The WebView2 x64 standalone installer is a full offline package, not the small bootstrapper. The script requires at least $minimumText and a valid Microsoft Authenticode signature before packaging it.

Download the 'Evergreen Standalone Installer' for x64 from:
https://developer.microsoft.com/microsoft-edge/webview2/

Save it as:
$Destination

Then rerun Build-Installer.ps1 -SkipPrerequisiteDownloads. The script will re-check the file size and signature before it is embedded. Do not use third-party download sites or disable signature validation.

Download attempts:
$details
"@
}

if ($env:OS -ne 'Windows_NT') {
    throw 'This script must run on Windows. WPF/.NET Framework and Inno Setup are Windows build tools.'
}
if (!(Test-Path $Solution)) { throw "Could not find solution: $Solution" }
if (!(Test-Path $IssFile)) { throw "Could not find Inno Setup script: $IssFile" }

Write-Step 'Checking build tools'
$msbuild = Find-MSBuild
if (!$msbuild) {
    throw 'MSBuild was not found. Install Visual Studio 2022 Community with the .NET desktop development workload and .NET Framework 4.8 Developer Pack.'
}
Write-Host "Using MSBuild: $msbuild" -ForegroundColor Green

Write-Step 'Building Harbor Browser (Release)'
$buildArgs = @($Solution, '/restore', '/m', '/p:Configuration=Release', '/p:Platform=Any CPU', "/p:Version=$Version", "/p:AssemblyVersion=$Version.0", "/p:FileVersion=$Version.$BuildRevision")
& $msbuild @buildArgs
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE" }

$BuildOutput = $BuildOutputCandidates | Where-Object { Test-Path (Join-Path $_ 'HarborBrowser.exe') } | Select-Object -First 1
if (!$BuildOutput) {
    throw "Build completed but HarborBrowser.exe was not found in: $($BuildOutputCandidates -join ', ')"
}
$exe = Join-Path $BuildOutput 'HarborBrowser.exe'

Write-Step 'Preparing application payload'
$absoluteProject = [System.IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\')
$absolutePayload = [System.IO.Path]::GetFullPath($PayloadDir)
if (!$absolutePayload.StartsWith($absoluteProject + '\dist\', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Payload path must stay within this project dist folder.' }
if ((Test-Path -LiteralPath $PayloadDir) -and ((Get-Item -LiteralPath $PayloadDir -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) { throw 'Payload folder must not be a link.' }
if (Test-Path $PayloadDir) { Remove-Item -LiteralPath $PayloadDir -Recurse -Force }
New-Item -ItemType Directory -Path $PayloadDir -Force | Out-Null
Get-ChildItem -LiteralPath $BuildOutput -Force | Where-Object {
    $_.Extension -notin @('.pdb', '.xml')
} | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $PayloadDir -Recurse -Force
}

$requiredFiles = @('HarborBrowser.exe', 'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.Wpf.dll')
foreach ($name in $requiredFiles) {
    if (!(Test-Path (Join-Path $PayloadDir $name))) {
        throw "Required build output is missing: $name. Check NuGet restore and the project build output."
    }
}
if (@(Get-ChildItem -Path $PayloadDir -Filter 'WebView2Loader.dll' -Recurse -File).Count -eq 0) {
    throw 'WebView2Loader.dll was not found in the build output. The WebView2 native loader must be packaged with Harbor.'
}

Write-Step 'Preparing offline prerequisites'
New-Item -ItemType Directory -Path $RedistDir -Force | Out-Null
if (!$SkipPrerequisiteDownloads) {
    # Official Microsoft links: .NET Framework 4.8 offline installer and WebView2 Evergreen x64 standalone installer.
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Download-AndVerify 'https://go.microsoft.com/fwlink/?linkid=2088631' $DotNetInstaller 'Microsoft Corporation' 1MB
    # LinkId 2124701 is Microsoft's Evergreen WebView2 x64 standalone (offline) installer.
    # LinkId 2124703 is the small online bootstrapper; do not package it as the offline runtime.
    # The standalone package is large and must pass both size and Authenticode checks.
    Download-AndVerify 'https://go.microsoft.com/fwlink/p/?LinkId=2124701' $WebViewInstaller 'Microsoft Corporation' 25MB
}

$prerequisiteChecks = @(
    @{ Path = $DotNetInstaller; MinimumBytes = 1MB; Name = '.NET Framework 4.8 offline installer' },
    @{ Path = $WebViewInstaller; MinimumBytes = 25MB; Name = 'WebView2 Evergreen x64 standalone installer' }
)
foreach ($item in $prerequisiteChecks) {
    $file = $item.Path
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Prerequisite package missing: $file. Run without -SkipPrerequisiteDownloads to download it from Microsoft, or add the official installer manually."
    }
    $length = (Get-Item -LiteralPath $file).Length
    if ($length -lt $item.MinimumBytes) {
        throw "Prerequisite package is too small: $($item.Name) is $(Format-Bytes $length), but at least $(Format-Bytes $item.MinimumBytes) is required. Do not package the WebView2 bootstrapper as the offline standalone runtime. See SETUP-README.md for the manual download instructions."
    }
    $sig = Get-AuthenticodeSignature -FilePath $file
    if ($sig.Status -ne 'Valid' -or !$sig.SignerCertificate -or $sig.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
        throw "Prerequisite package signature check failed: $file. Status: $($sig.Status). The file will not be packaged."
    }
    Write-Host "Verified $($item.Name): $(Format-Bytes $length), Microsoft signature valid." -ForegroundColor Green
}

if ($SkipCompile) {
    Write-Host "`nInstaller payload is staged. Skipped Inno Setup compilation by request." -ForegroundColor Green
    exit 0
}

Write-Step 'Locating Inno Setup Compiler'
$isccCandidates = @(
    # Per-user installs (for example, winget installs without elevation).
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    # Machine-wide installs.
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)
$isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
$iscc = if ($isccCommand) { $isccCommand.Source } else { $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1 }
if (!$iscc) {
    throw "Inno Setup Compiler was not found. Install it with: winget install --id JRSoftware.InnoSetup.7 -e"
}
Write-Host "Using Inno Setup: $iscc" -ForegroundColor Green

Write-Step 'Compiling the single-file setup wizard'
Push-Location (Split-Path -Parent $IssFile)
try {
    & $iscc "/DAppVersion=$Version" "/DBuildRevision=$BuildRevision" "/DReleaseSuffix=$ReleaseSuffix" 'HarborBrowser.iss'
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed with exit code $LASTEXITCODE" }
} finally {
    Pop-Location
}

$setup = Join-Path $ProjectRoot "dist\HarborBrowserSetup-$Version$ReleaseSuffix.exe"
if (!(Test-Path $setup)) { throw "Inno Setup did not produce the expected file: $setup" }
Write-Host "`nSUCCESS: $setup" -ForegroundColor Green
Write-Host "This installer includes Harbor, .NET Framework 4.8 offline setup, and the WebView2 Evergreen x64 standalone installer."
