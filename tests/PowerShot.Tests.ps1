Describe "PowerShot.ps1" {
    It "Should exist" {
        Test-Path "$PSScriptRoot\..\src\PowerShot.ps1" | Should Be $true
    }

    It "Should contain the main entry point logic" {
        $content = Get-Content "$PSScriptRoot\..\src\PowerShot.ps1" -Raw
        $content | Should Match "Add-Type"
        $content | Should Match "Windows.Forms"
    }
}

Describe "Viewer.ps1" {
    It "Should exist" {
        Test-Path "$PSScriptRoot\..\src\Viewer.ps1" | Should Be $true
    }
}
