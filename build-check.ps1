# ============================================================
# PowerShot Build Check
# C# compilation verification via Add-Type (same path as PowerShot.ps1)
# Usage: powershell -File build-check.ps1
# ============================================================

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Xaml
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$srcPath = Join-Path $PSScriptRoot "src"
$csFiles = (Get-ChildItem -Path $srcPath -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch "ViewerController|ViewerModels" }).FullName

if (-not $csFiles -or $csFiles.Count -eq 0) {
    Write-Host "ERROR: No C# source files found in $srcPath" -ForegroundColor Red
    exit 1
}

Write-Host "Compiling $($csFiles.Count) C# files..." -ForegroundColor Cyan

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
    Write-Host "OK: Compilation succeeded." -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "ERROR: C# Compilation Failed:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if ($_.Exception.InnerException) {
        Write-Host $_.Exception.InnerException.Message -ForegroundColor Yellow
    }
    exit 1
}
