# Quick Test Script for Kilo Extension

Write-Host "=== Kilo Extension UI Verification Script ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check if project builds
Write-Host "[1/5] Testing if extension builds..." -ForegroundColor Yellow
$buildResult = dotnet build "Kilo.VisualStudio.Extension/Kilo.VisualStudio.Extension.csproj" -c Release -v quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Extension builds successfully" -ForegroundColor Green
} else {
    Write-Host "  ✗ Extension failed to build" -ForegroundColor Red
    Write-Host "  Run: dotnet build Kilo.VisualStudio.Extension/Kilo.VisualStudio.Extension.csproj" -ForegroundColor Gray
    exit 1
}

# Test 2: Check if VSIX manifest exists
Write-Host "[2/5] Checking VSIX manifest..." -ForegroundColor Yellow
$manifestPath = "Kilo.VisualStudio.Extension\source.extension.vsixmanifest"
if (Test-Path $manifestPath) {
    Write-Host "  ✓ Manifest found" -ForegroundColor Green
    [xml]$manifest = Get-Content $manifestPath
    $displayName = $manifest.PackageManifest.Metadata.DisplayName
    Write-Host "    Extension name: $displayName" -ForegroundColor Gray
} else {
    Write-Host "  ✗ Manifest not found" -ForegroundColor Red
}

# Test 3: Check if commands are registered
Write-Host "[3/5] Checking command registrations..." -ForegroundColor Yellow
$vsctPath = "Kilo.VisualStudio.Extension\KiloCommands.vsct"
if (Test-Path $vsctPath) {
    $vsctContent = Get-Content $vsctPath -Raw
    if ($vsctContent -match "Open Kilo Assistant") {
        Write-Host "  ✓ 'Open Kilo Assistant' command found in VSCT" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Opening command not found in VSCT" -ForegroundColor Red
    }
    
    # Count how many commands are defined
    $commandCount = ([regex]::Matches($vsctContent, "<Button guid=")).Count
    Write-Host "    Total commands defined: $commandCount" -ForegroundColor Gray
} else {
    Write-Host "  ✗ VSCT file not found" -ForegroundColor Red
}

# Test 4: Check tool window registration
Write-Host "[4/5] Checking tool window registration..." -ForegroundColor Yellow
$packagePath = "Kilo.VisualStudio.Extension\KiloPackage.cs"
if (Test-Path $packagePath) {
    $packageContent = Get-Content $packagePath -Raw
    if ($packageContent -match "ProvideToolWindow.*KiloAssistantToolWindowPane") {
        Write-Host "  ✓ Tool window registered in package" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Tool window registration not found" -ForegroundColor Red
    }
} else {
    Write-Host "  ✗ Package file not found" -ForegroundColor Red
}

# Test 5: Check for common issues
Write-Host "[5/5] Checking for potential issues..." -ForegroundColor Yellow
$issues = @()

# Check if Resources folder exists (for icons)
if (!(Test-Path "Kilo.VisualStudio.Extension\Resources")) {
    $issues += "No Resources folder (icons might be missing)"
}

# Check if there are compilation errors
$errors = dotnet build "Kilo.VisualStudio.Extension/Kilo.VisualStudio.Extension.csproj" -c Release 2>&1 | Select-String "error"
if ($errors) {
    $issues += "Compilation errors detected"
}

if ($issues.Count -eq 0) {
    Write-Host "  ✓ No obvious issues detected" -ForegroundColor Green
} else {
    Write-Host "  ⚠ Potential issues found:" -ForegroundColor Yellow
    foreach ($issue in $issues) {
        Write-Host "    - $issue" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=== Instructions to Access UI ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "After installing the extension in Visual Studio 2022:" -ForegroundColor White
Write-Host "1. Open Visual Studio 2022" -ForegroundColor Gray
Write-Host "2. Go to: Tools → Open Kilo Assistant" -ForegroundColor Green
Write-Host "3. Or press: Ctrl + Shift + K" -ForegroundColor Green
Write-Host ""
Write-Host "If you don't see it:" -ForegroundColor White
Write-Host "- Check: Extensions → Manage Extensions → Installed tab" -ForegroundColor Gray
Write-Host "- Look for: 'Kilo Visual Studio Assistant'" -ForegroundColor Gray
Write-Host "- Make sure it's enabled (not disabled)" -ForegroundColor Gray
Write-Host "- Try restarting Visual Studio" -ForegroundColor Gray
Write-Host ""
Write-Host "For detailed troubleshooting, see:" -ForegroundColor White
Write-Host "  → TROUBLESHOOTING_UI_ACCESS.md" -ForegroundColor Cyan
Write-Host ""

# Check if running in debug/release
$binPath = "Kilo.VisualStudio.Extension\bin\Release"
if (Test-Path $binPath) {
    Write-Host "Build output location:" -ForegroundColor White
    Write-Host "  $binPath" -ForegroundColor Gray
    
    # Look for VSIX file
    $vsixFiles = Get-ChildItem -Path $binPath -Filter "*.vsix" -Recurse -ErrorAction SilentlyContinue
    if ($vsixFiles) {
        Write-Host ""
        Write-Host "VSIX installer found at:" -ForegroundColor Green
        foreach ($vsix in $vsixFiles) {
            Write-Host "  $($vsix.FullName)" -ForegroundColor Cyan
            Write-Host "  Double-click this file to install!" -ForegroundColor Yellow
        }
    } else {
        Write-Host ""
        Write-Host "To generate VSIX installer, you may need to:" -ForegroundColor Yellow
        Write-Host "  1. Build in Release mode" -ForegroundColor Gray
        Write-Host "  2. Ensure VSIX project settings are correct" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
