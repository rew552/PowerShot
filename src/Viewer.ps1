$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

$csFiles = @(
    (Join-Path $scriptDir "Controllers\ViewerController.cs"),
    (Join-Path $scriptDir "Models\ViewerModels.cs")
)

Add-Type -Path $csFiles -ReferencedAssemblies @(
    "System.Core",
    "System.Xml",
    "System.Runtime.Serialization"
)

[PowerShot.ViewerController]::Run($projectDir)
