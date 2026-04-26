# ============================================================
# PowerShot v3.1 - PowerShell Launcher & Session Manager
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
$scriptPath = Split-Path $MyInvocation.MyCommand.Path -Parent

# --- Recompile Guard: only Add-Type if not already loaded ---
if (-not ('PowerShot.Program' -as [type])) {
    $csFiles = (Get-ChildItem -Path $scriptPath -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch "ViewerController|ViewerModels" }).FullName

    if (-not $csFiles -or $csFiles.Count -eq 0) {
        Write-Host "ERROR: No C# source files found in $scriptPath" -ForegroundColor Red
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
        "System.Runtime.Serialization, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
        "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
        "System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
    )

    try {
        Add-Type -Path $csFiles -ReferencedAssemblies $refs -ErrorAction Stop
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
[PowerShot.App.Program]::Run($scriptPath)
