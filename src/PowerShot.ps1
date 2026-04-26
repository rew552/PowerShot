# ============================================================
# PowerShot v3.2 - PowerShell Launcher & Session Manager
# ============================================================

# --- Resolve Paths ---
$scriptPath = Split-Path $MyInvocation.MyCommand.Path -Parent
$cacheDir = Join-Path $scriptPath ".cache"
if (-not (Test-Path $cacheDir)) { New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null }

# --- Load WPF and Drawing Assemblies ---
$assemblies = @("PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml", "System.Drawing", "System.Windows.Forms")
foreach ($asm in $assemblies) { Add-Type -AssemblyName $asm }

# --- Recompile Guard & Caching Logic ---
if (-not ('PowerShot.App.Program' -as [type])) {
    # Get CS files (optimized search)
    $csFiles = Get-ChildItem -Path $scriptPath -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch "ViewerController|ViewerModels" }
    
    if (-not $csFiles) {
        Write-Host "ERROR: No C# source files found in $scriptPath" -ForegroundColor Red
        Read-Host "Press Enter to exit"; exit 1
    }

    # Generate Hash for Caching based on filenames and last modification dates
    $hashInput = ($csFiles | ForEach-Object { $_.FullName + $_.LastWriteTime.Ticks }) -join "|"
    $hash = [BitConverter]::ToString(([System.Security.Cryptography.SHA1]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($hashInput)))).Replace("-", "").Substring(0, 12)
    $cacheDll = Join-Path $cacheDir "PowerShot_$hash.dll"

    if (Test-Path $cacheDll) {
        try {
            Add-Type -Path $cacheDll -ErrorAction Stop
        } catch {
            Remove-Item $cacheDll -Force
        }
    }

    # If types are still not loaded, compile
    if (-not ('PowerShot.App.Program' -as [type])) {
        Write-Host "Preparing PowerShot (First run or update)..." -ForegroundColor Cyan
        $refs = @(
            "PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml", 
            "System.Drawing", "System.Windows.Forms", "System.Xml", 
            "System.Runtime.Serialization", "System", "System.Core"
        )
        try {
            # Clean up old cache files to keep it tidy
            Get-ChildItem $cacheDir -Filter "PowerShot_*.dll" | Remove-Item -Force -ErrorAction SilentlyContinue
            
            Add-Type -Path $csFiles.FullName -ReferencedAssemblies $refs -OutputAssembly $cacheDll -OutputType Library -ErrorAction Stop
            # After compilation, load the newly created DLL
            Add-Type -Path $cacheDll
        } catch {
            Write-Host "ERROR: C# Compilation Failed:`n$($_.Exception.Message)" -ForegroundColor Red
            if ($_.Exception.InnerException) { Write-Host $_.Exception.InnerException.Message -ForegroundColor Yellow }
            Read-Host "Press Enter to exit"; exit 1
        }
    }
}

# --- Launch Application ---
[PowerShot.App.Program]::Run($scriptPath)
