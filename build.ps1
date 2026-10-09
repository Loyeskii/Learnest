<#
    Learnest - build script
    ---------------------------------------------------------------
    Builds Learnest.exe and (optionally) the installer.

    Requires the .NET SDK (8 or 9) with the Windows Desktop workload:
        winget install --id Microsoft.DotNet.SDK.8 -e

    Usage:
        pwsh -File build.ps1                       # Release build into .\dist
        pwsh -File build.ps1 -Configuration Debug
        pwsh -File build.ps1 -SelfTest             # build then run the headless self test
        pwsh -File build.ps1 -UiCheck              # build then construct the whole UI headlessly
        pwsh -File build.ps1 -Run                  # build then launch the app
        pwsh -File build.ps1 -Publish              # self-contained publish (no .NET runtime needed)
        pwsh -File build.ps1 -Installer            # build + compile the Inno Setup installer
        pwsh -File build.ps1 -Clean                # remove bin/obj/dist first
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory,
    [switch]$Run,
    [switch]$SelfTest,
    [switch]$UiCheck,
    [switch]$LangCheck,
    [switch]$Publish,
    [switch]$Installer,
    [switch]$Clean,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'dist' }
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$project = Join-Path $root 'src\Learnest.csproj'

function Write-Step([string]$text) { if (-not $Quiet) { Write-Host "==> $text" -ForegroundColor Cyan } }
function Write-Detail([string]$text) { if (-not $Quiet) { Write-Host "    $text" -ForegroundColor DarkGray } }
function Fail([string]$text) { Write-Host $text -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------- toolchain check
# The SDK is mandatory: Learnest targets net8.0-windows with UseWPF, which the in-box
# .NET Framework compiler cannot build.  If it is missing, say so and stop - do not fall
# back to an older toolchain.
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue)
if (-not $dotnet) {
    Fail @'
The .NET SDK was not found on PATH.

Install it with:
    winget install --id Microsoft.DotNet.SDK.8 -e

or download it from https://dotnet.microsoft.com/download/dotnet/8.0
(choose "SDK x64", not just the runtime).
'@
}
$sdks = @(& $dotnet.Source --list-sdks 2>$null | Where-Object { $_ -match '^\d+\.' })
if ($sdks.Count -eq 0) {
    Fail @'
`dotnet` is present but no SDK is installed (only runtimes were found).
`dotnet --list-sdks` must report a version such as 8.0.425.

Install the SDK with:
    winget install --id Microsoft.DotNet.SDK.8 -e
'@
}
Write-Step ("Toolchain: dotnet SDK " + ($sdks[0] -split ' ')[0])

# ---------------------------------------------------------------- clean
if ($Clean) {
    Write-Step 'Cleaning build output'
    foreach ($path in @((Join-Path $root 'src\bin'), (Join-Path $root 'src\obj'), $OutputDirectory)) {
        if (Test-Path $path) { Remove-Item $path -Recurse -Force; Write-Detail "removed $path" }
    }
}

# ---------------------------------------------------------------- build
Write-Step "Building $Configuration -> $OutputDirectory"
$buildArgs = @('build', $project, '-c', $Configuration, '-o', $OutputDirectory, '--nologo')
$buildOutput = & $dotnet.Source @buildArgs 2>&1
$errors = @($buildOutput | Where-Object { $_ -match ': error ' })
if (-not $Quiet) {
    $buildOutput | Where-Object { $_ -match ': warning ' } | Select-Object -First 15 | ForEach-Object { Write-Detail $_ }
}
if ($errors.Count -gt 0) {
    $errors | Select-Object -First 25 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkYellow }
    Fail "Build failed with $($errors.Count) error(s)."
}
if ($buildOutput -match '已成功|Build succeeded') { Write-Detail 'build succeeded' }

$exe = Join-Path $OutputDirectory 'Learnest.exe'
if (-not (Test-Path $exe)) { Fail "Build reported success but $exe was not produced." }
Write-Host ("Built: {0} ({1} KB)" -f $exe, [math]::Round((Get-Item $exe).Length / 1KB, 1)) -ForegroundColor Green

# ---------------------------------------------------------------- self-contained publish
if ($Publish) {
    $publishDir = Join-Path $root 'dist-standalone'
    Write-Step "Publishing self-contained build -> $publishDir"
    $publishArgs = @('publish', $project, '-c', $Configuration, '-r', 'win-x64',
        '--self-contained', 'true', '-p', 'PublishSingleFile=false',
        '-p:DebugType=none', '-o', $publishDir, '--nologo')
    $publishOutput = & $dotnet.Source @publishArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        $publishOutput | Select-Object -Last 20 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkYellow }
        Fail 'dotnet publish failed.'
    }
    $size = (Get-ChildItem $publishDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
    Write-Host ("Published: {0} ({1} MB total)" -f $publishDir, [math]::Round($size / 1MB, 1)) -ForegroundColor Green
}

# ---------------------------------------------------------------- checks
if ($SelfTest) {
    Write-Step 'Running headless self test'
    & $exe --selftest
    $code = $LASTEXITCODE
    Write-Detail "exit code $code (report: $env:TEMP\learnest-selftest.log)"
    if ($code -ne 0) { exit $code }
}
if ($UiCheck) {
    Write-Step 'Constructing the whole UI headlessly'
    & $exe --uicheck
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if ($LangCheck) {
    Write-Step 'Verifying embedded language packs'
    & $exe --langcheck
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

# ---------------------------------------------------------------- installer
if ($Installer) {
    Write-Step 'Compiling the Inno Setup installer'
    $iscc = (Get-Command ISCC -ErrorAction SilentlyContinue)
    if (-not $iscc) {
        foreach ($candidate in @(
                (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
                (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
                (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'))) {
            if (Test-Path $candidate) { $iscc = Get-Item $candidate; break }
        }
    }
    if (-not $iscc) {
        Fail @'
Inno Setup 6 (ISCC.exe) was not found.

Install it with:
    winget install --id JRSoftware.InnoSetup -e

or download it from https://jrsoftware.org/isdl.php
'@
    }
    $isccPath = if ($iscc.Source) { $iscc.Source } else { $iscc.FullName }
    & $isccPath (Join-Path $root 'installer\Learnest.iss') | Select-Object -Last 4 | ForEach-Object { Write-Detail $_ }
    if ($LASTEXITCODE -ne 0) { Fail 'ISCC failed.' }
    $setup = Join-Path $OutputDirectory 'installer\Learnest-1.0.0-Setup.exe'
    if (Test-Path $setup) {
        Write-Host ("Installer: {0} ({1} MB)" -f $setup, [math]::Round((Get-Item $setup).Length / 1MB, 2)) -ForegroundColor Green
    }
}

if ($Run) {
    Write-Step 'Launching Learnest'
    Start-Process -FilePath $exe
}
