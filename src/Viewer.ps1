$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$logicPath = Join-Path $scriptDir "ViewerLogic.cs"

Add-Type -TypeDefinition (Get-Content $logicPath -Raw) -ReferencedAssemblies @(
    "System.Core",
    "System.Xml",
    "System.Runtime.Serialization"
)

[PowerShot.ViewerLogic]::Run($projectDir)
