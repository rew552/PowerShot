# ============================================================
# PowerShot v2.0 - PowerShell Launcher & Session Manager
# ============================================================
# WPF hybrid architecture: XAML + C# compiled in-memory via Add-Type
# Requires STA thread (launched via PowerShot.bat with -STA flag)
# ============================================================

# --- Load WPF and Drawing Assemblies ---
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Xaml
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# --- Resolve Paths ---
$scriptPath  = Split-Path $MyInvocation.MyCommand.Path -Parent
$csLogicPath = Join-Path $scriptPath "PowerShotLogic.cs"
$csNamingPath = Join-Path $scriptPath "FileNamingLogic.cs"
$saveDir     = Join-Path $scriptPath "..\Screenshots"

# --- Recompile Guard: only Add-Type if not already loaded ---
if (-not ('PowerShot.Program' -as [type])) {
    if (-not (Test-Path $csLogicPath)) {
        Write-Host "ERROR: PowerShotLogic.cs not found: $csLogicPath" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
    if (-not (Test-Path $csNamingPath)) {
        Write-Host "ERROR: FileNamingLogic.cs not found: $csNamingPath" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }

    # Referenced assemblies for WPF + Drawing + Interop
    $refs = @(
        "PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"
        "PresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"
        "WindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"
        "System.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
        "System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
        "System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
        "System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
        "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
        "System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
    )

    try {
        Add-Type -Path @($csNamingPath, $csLogicPath) -ReferencedAssemblies $refs -ErrorAction Stop
    }
    catch {
        Write-Host "ERROR: C# Compilation Failed:" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        if ($_.Exception.InnerException) {
            Write-Host $_.Exception.InnerException.Message -ForegroundColor Yellow
        }
        Read-Host "Press Enter to exit"
        exit 1
    }
}
else {
    Write-Host "C# Code already compiled. Skipping." -ForegroundColor Cyan
}

# --- Launch Application ---
[PowerShot.Program]::Run($scriptPath, $saveDir)
