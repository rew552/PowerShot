# ============================================================
# PowerShot Build Check
# C# compilation verification via Add-Type (same path as PowerShot.ps1)
# Usage: powershell -File tests/build-check.ps1
# ============================================================

$assemblies = @("PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml", "System.Drawing", "System.Windows.Forms")
foreach ($asm in $assemblies) { Add-Type -AssemblyName $asm }

$srcPath = Join-Path $PSScriptRoot ".." "src"
$csFiles = Get-ChildItem -Path $srcPath -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch "ViewerController|ViewerModels" }

if (-not $csFiles) {
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
    Add-Type -Path $csFiles.FullName -ReferencedAssemblies $refs -ErrorAction Stop
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
