#Requires -Version 5.1
<#
    Learnest - installer build wrapper              installer\Build-Installer.ps1
    ========================================================================
    Builds the program (unless -SkipBuild) and then compiles
    installer\Learnest.iss with the Inno Setup 6 command line compiler.

        pwsh -File installer\Build-Installer.ps1
        pwsh -File installer\Build-Installer.ps1 -SkipBuild
        pwsh -File installer\Build-Installer.ps1 -Quiet

    (Windows PowerShell works as well:
        powershell -ExecutionPolicy Bypass -File installer\Build-Installer.ps1 )

    What it does
        1. Runs ..\build.ps1 so that ..\dist is up to date.  With -SkipBuild
           the existing ..\dist folder is used as it is.
        2. Fails with a clear message when ..\dist\Learnest.exe is missing.
        3. Locates ISCC.exe, the Inno Setup 6 compiler, in this order:
               C:\Program Files (x86)\Inno Setup 6\ISCC.exe
               C:\Program Files\Inno Setup 6\ISCC.exe
               the same folders derived from %ProgramFiles% / %LOCALAPPDATA%
               the winget package folder
                   %LOCALAPPDATA%\Microsoft\WinGet\Packages\JRSoftware.InnoSetup*
               PATH (Get-Command ISCC.exe)
        4. If the compiler cannot be found it prints bilingual guidance - the
           winget install command and the download URL - and exits with a
           non-zero exit code.  It never downloads or installs anything.
        5. On success it reports the produced installer path and its size.

    Notes
        * Inno Setup is NOT part of this repository:  winget install --id
          JRSoftware.InnoSetup -e   or   https://jrsoftware.org/isdl.php
        * PowerShell 5.1 compatible: no ternary operator, no "??"/"??=", no
          Add-Type, no COM objects - cmdlets and a few core types only.
        * The installer is optional.  ..\dist is already a runnable, xcopy
          deployable program; see ..\docs\BUILD.md.
#>
[CmdletBinding()]
param(
    # Do not rebuild ..\dist first; package what is already there.
    [switch]$SkipBuild,

    # Print as little as possible and pass /Q (silent) instead of /Qp to ISCC.
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

# --------------------------------------------------------------------- output
function Write-Step {
    param([string]$Text)
    if (-not $Quiet) { Write-Host $Text -ForegroundColor Cyan }
}

function Write-Detail {
    param([string]$Text)
    if (-not $Quiet) { Write-Host ('    ' + $Text) -ForegroundColor DarkGray }
}

function Write-Problem {
    param([string]$Text)
    Write-Host $Text -ForegroundColor Red
}

function Write-WarningLine {
    param([string]$Text)
    Write-Host $Text -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Text)
    Write-Host $Text -ForegroundColor Green
}

# ---------------------------------------------------------------------- paths
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path }

$root = Split-Path -Parent $scriptDir                     # ...\Learnest
$distDir = Join-Path $root 'dist'
$appExe = Join-Path $distDir 'Learnest.exe'
$issPath = Join-Path $scriptDir 'Learnest.iss'
$setupDir = Join-Path $distDir 'installer'                # Learnest.iss: OutputDir=..\dist\installer
$setupPath = Join-Path $setupDir 'Learnest-1.0.0-Setup.exe'

# ----------------------------------------------------------- find the compiler
function Find-Iscc {
    # 1) the two documented Inno Setup 6 locations
    $candidates = @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )
    # 2) the same layout when Program Files lives on another drive / language
    if (${env:ProgramFiles(x86)}) { $candidates += (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe') }
    if ($env:ProgramFiles) { $candidates += (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe') }
    if ($env:LOCALAPPDATA) { $candidates += (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe') }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }

    # 3) winget installs into %LOCALAPPDATA%\Microsoft\WinGet\Packages\<PackageId>_<version>
    if ($env:LOCALAPPDATA) {
        $wingetRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
        if (Test-Path -LiteralPath $wingetRoot -PathType Container) {
            $packages = Get-ChildItem -LiteralPath $wingetRoot -Directory -Filter 'JRSoftware.InnoSetup*' -ErrorAction SilentlyContinue
            foreach ($package in $packages) {
                $found = Get-ChildItem -LiteralPath $package.FullName -Recurse -Filter 'ISCC.exe' -File -ErrorAction SilentlyContinue |
                         Select-Object -First 1
                if ($found) { return $found.FullName }
            }
        }
    }

    # 4) PATH
    $command = Get-Command -Name 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) {
        if ($command.Source) { return $command.Source }
        if ($command.Path) { return $command.Path }
        if ($command.Definition) { return $command.Definition }
    }

    return $null
}

# ------------------------------------------------------------------ 0. sanity
if (-not (Test-Path -LiteralPath $issPath -PathType Leaf)) {
    Write-Problem ('找不到脚本 / script not found: ' + $issPath)
    exit 1
}

# ------------------------------------------------------------------- 1. build
if ($SkipBuild) {
    Write-Step '==> 跳过构建，直接打包现有 dist / -SkipBuild: packaging the existing dist folder'
} else {
    $buildScript = Join-Path $root 'build.ps1'
    if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
        Write-Problem ('找不到构建脚本 / build script not found: ' + $buildScript)
        exit 1
    }

    Write-Step ('==> 构建程序 / building the program: ' + $buildScript)

    $exitCode = 0
    $powerShellExe = $null
    if ($env:SystemRoot) {
        $powerShellExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    }

    if ($powerShellExe -and (Test-Path -LiteralPath $powerShellExe -PathType Leaf)) {
        # Run the build in a child process so that its exit code and its
        # $ErrorActionPreference stay separate from this script.
        $buildArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $buildScript)
        if ($Quiet) { $buildArguments += '-Quiet' }
        & $powerShellExe @buildArguments
        $exitCode = $LASTEXITCODE
    } else {
        # Fallback for hosts without in-box Windows PowerShell.
        & $buildScript
        if ($?) { $exitCode = 0 } else { $exitCode = 1 }
    }

    if ($exitCode -ne 0) {
        Write-Problem ('构建失败 / build.ps1 failed with exit code ' + $exitCode + '.')
        Write-WarningLine '安装包未生成。/ No installer was produced.'
        exit $exitCode
    }
}

# ------------------------------------------- 2. did the build produce the app?
if (-not (Test-Path -LiteralPath $appExe -PathType Leaf)) {
    Write-Problem ('缺少 / missing: ' + $appExe)
    Write-WarningLine '    Learnest.exe 还没有构建好，没有可打包的程序。'
    Write-WarningLine '    Learnest.exe has not been built, so there is nothing to package.'
    Write-WarningLine '    请运行 build.ps1（不要加 -SkipBuild）后重试。/ Re-run without -SkipBuild, or check the build output above.'
    exit 1
}

$appInfo = Get-Item -LiteralPath $appExe
Write-Detail ('程序 / app: ' + $appInfo.FullName + '  (' + $appInfo.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss') + ')')

# ------------------------------------------ 3. locate the Inno Setup compiler
$iscc = Find-Iscc
if (-not $iscc) {
    Write-Problem '找不到 Inno Setup 6 的编译器 ISCC.exe。'
    Write-Problem 'The Inno Setup 6 compiler (ISCC.exe) was not found.'
    Write-Host ''
    Write-WarningLine '  请自行安装（本脚本不会下载或安装任何东西）：'
    Write-WarningLine '  Install it yourself - this script never downloads anything:'
    Write-Host '      winget install --id JRSoftware.InnoSetup -e'
    Write-Host '      https://jrsoftware.org/isdl.php'
    Write-Host ''
    Write-WarningLine '  安装完成后重新运行本脚本 / re-run this script after installing Inno Setup 6.'
    Write-WarningLine '  安装包是可选的：dist 文件夹本身已经可以直接运行与复制部署。'
    Write-WarningLine '  The installer is optional: the dist folder is already runnable and xcopy deployable.'
    exit 1
}

# --------------------------------------------------------------- 4. compile
Write-Step ('==> 编译安装包 / compiling the installer with: ' + $iscc)
$isccArguments = @()
if ($Quiet) { $isccArguments += '/Q' } else { $isccArguments += '/Qp' }
$isccArguments += $issPath
& $iscc @isccArguments
$isccExit = $LASTEXITCODE

if ($isccExit -ne 0) {
    Write-Problem ('Inno Setup 编译失败 / ISCC.exe failed with exit code ' + $isccExit + '.')
    Write-Host ('    脚本 / script: ' + $issPath)
    Write-Host '    常见原因 / usual causes:'
    Write-Host '      - Inno Setup 版本低于 6.3（ArchitecturesInstallIn64BitMode=x64compatible）'
    Write-Host '        Inno Setup older than 6.3 does not know x64compatible.'
    Write-Host '      - ..\dist 为空或缺少 Learnest.exe / empty or incomplete ..\dist'
    Write-Host '      - 路径含特殊字符或没有写权限 / odd paths or missing write permission'
    exit $isccExit
}

# ---------------------------------------------------------------- 5. report
$installer = $null
if (Test-Path -LiteralPath $setupPath -PathType Leaf) {
    $installer = Get-Item -LiteralPath $setupPath
} elseif (Test-Path -LiteralPath $setupDir -PathType Container) {
    $installer = Get-ChildItem -LiteralPath $setupDir -Filter '*-Setup.exe' -File -ErrorAction SilentlyContinue |
                 Sort-Object -Property LastWriteTime -Descending |
                 Select-Object -First 1
}

if (-not $installer) {
    Write-Problem ('编译结束但找不到安装包 / the compiler finished but no setup executable was found in ' + $setupDir + '.')
    exit 1
}

$bytes = $installer.Length
$kilobytes = '{0:N1}' -f ($bytes / 1KB)
$megabytes = '{0:N1}' -f ($bytes / 1MB)

Write-Host ''
Write-Success '安装包已生成 / installer built:'
Write-Success ('    ' + $installer.FullName)
Write-Success ('    大小 / size: ' + $megabytes + ' MB (' + $kilobytes + ' KB / ' + $bytes + ' bytes)')
Write-Host ''
Write-Host '  静默安装示例 / silent install example:' -ForegroundColor DarkGray
Write-Host ('      "' + $installer.Name + '" /VERYSILENT /CURRENTUSER /NORESTART') -ForegroundColor DarkGray
Write-Host '  提醒 / reminder: 安装包只包含 dist 的内容；Learnest 本身不保存业务数据，' -ForegroundColor DarkGray
Write-Host '  首次启动时由用户创建或打开自己的容器文件夹。' -ForegroundColor DarkGray
exit 0
