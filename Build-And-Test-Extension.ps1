param(
    [ValidateSet("build", "install", "debug", "all")]
    [string]$Mode = "all",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$RootSuffix = "Exp",
    [switch]$CleanBeforeBuild
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[Kilo] $Message" -ForegroundColor Cyan
}

function Find-VsInstallPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe not found. Install Visual Studio Installer first."
    }

    $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if (-not $path) {
        throw "No Visual Studio installation with MSBuild found."
    }

    return $path.Trim()
}

function Invoke-SafeClean {
    param([string]$RepoRoot)

    $resolvedRoot = [System.IO.Path]::GetFullPath($RepoRoot)
    Write-Step "Cleaning bin/obj folders under: $resolvedRoot"

    $folders = Get-ChildItem -Path $resolvedRoot -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -in @("bin", "obj") }

    foreach ($folder in $folders) {
        $folderPath = [System.IO.Path]::GetFullPath($folder.FullName)
        $isUnderRoot = $folderPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)
        $isExpectedName = ($folder.Name -eq "bin" -or $folder.Name -eq "obj")

        if (-not $isUnderRoot -or -not $isExpectedName) {
            throw "Unsafe clean target blocked: $folderPath"
        }

        Remove-Item -LiteralPath $folderPath -Recurse -Force -ErrorAction Stop
    }

    Write-Host "Removed $($folders.Count) bin/obj folders." -ForegroundColor Green
}

function Build-Extension {
    param(
        [string]$VsInstallPath,
        [string]$Configuration
    )

    $msbuild = Join-Path $VsInstallPath "MSBuild\Current\Bin\MSBuild.exe"
    if (-not (Test-Path $msbuild)) {
        throw "MSBuild not found at: $msbuild"
    }

    $extensionProject = Join-Path $PSScriptRoot "Kilo.VisualStudio.Extension\Kilo.VisualStudio.Extension.csproj"
    $vsixProject = Join-Path $PSScriptRoot "Kilo.VisualStudio.Vsix\Kilo.VisualStudio.Vsix.csproj"
    if (-not (Test-Path $extensionProject)) {
        throw "Extension project not found: $extensionProject"
    }
    if (-not (Test-Path $vsixProject)) {
        throw "VSIX project not found: $vsixProject"
    }

    Write-Step "Building extension library ($Configuration)..."
    & $msbuild $extensionProject /t:Restore,Build /p:Configuration=$Configuration /p:DeployExtension=false /m
    if ($LASTEXITCODE -ne 0) {
        throw "Extension library build failed with exit code $LASTEXITCODE."
    }

    Write-Step "Packaging VSIX ($Configuration)..."
    & $msbuild $vsixProject /t:Restore,Build /p:Configuration=$Configuration /p:DeployExtension=false /m
    if ($LASTEXITCODE -ne 0) {
        throw "VSIX packaging build failed with exit code $LASTEXITCODE."
    }
}

function Find-VsixFile {
    param([string]$Configuration)

    $searchRoot = Join-Path $PSScriptRoot "Kilo.VisualStudio.Vsix\bin\$Configuration"
    if (-not (Test-Path $searchRoot)) {
        throw "Build output folder not found: $searchRoot"
    }

    $vsix = Get-ChildItem -Path $searchRoot -Filter *.vsix -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $vsix) {
        throw "No VSIX file found under: $searchRoot"
    }

    return $vsix.FullName
}

function Install-Vsix {
    param(
        [string]$VsInstallPath,
        [string]$VsixPath
    )

    $installer = Join-Path $VsInstallPath "Common7\IDE\VSIXInstaller.exe"
    if (-not (Test-Path $installer)) {
        throw "VSIXInstaller.exe not found at: $installer"
    }

    Write-Step "Installing VSIX..."
    & $installer $VsixPath
    if ($LASTEXITCODE -ne 0) {
        throw "VSIX installation failed with exit code $LASTEXITCODE."
    }
}

function Launch-ExperimentalVs {
    param(
        [string]$VsInstallPath,
        [string]$RootSuffix
    )

    $devenv = Join-Path $VsInstallPath "Common7\IDE\devenv.exe"
    if (-not (Test-Path $devenv)) {
        throw "devenv.exe not found at: $devenv"
    }

    Write-Step "Launching Visual Studio /rootsuffix $RootSuffix /log ..."
    Start-Process -FilePath $devenv -ArgumentList "/rootsuffix $RootSuffix /log"
    Write-Host "Activity log: %APPDATA%\Microsoft\VisualStudio\17.0_$RootSuffix\ActivityLog.xml" -ForegroundColor Yellow
}

try {
    $vsInstallPath = Find-VsInstallPath
    Write-Step "Using VS install: $vsInstallPath"

    if ($Mode -in @("build", "install", "all")) {
        if ($CleanBeforeBuild) {
            Invoke-SafeClean -RepoRoot $PSScriptRoot
        }

        Build-Extension -VsInstallPath $vsInstallPath -Configuration $Configuration
        $vsixPath = Find-VsixFile -Configuration $Configuration
        Write-Host "VSIX: $vsixPath" -ForegroundColor Green
    }

    if ($Mode -in @("install", "all")) {
        Install-Vsix -VsInstallPath $vsInstallPath -VsixPath $vsixPath
    }

    if ($Mode -in @("debug", "all")) {
        Launch-ExperimentalVs -VsInstallPath $vsInstallPath -RootSuffix $RootSuffix
    }
}
catch {
    Write-Error $_
    exit 1
}
