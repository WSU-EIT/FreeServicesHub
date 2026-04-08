<#
.SYNOPSIS
    Fork and rename a FreeCRM project in one command.

.DESCRIPTION
    This script automates the complete FreeCRM fork workflow:
    1. Clones the FreeCRM repository from GitHub
    2. Runs the module removal tool with specified selections
    3. Runs the rename tool to change project name
    4. Copies the result to your specified output directory

.PARAMETER NewName
    The new name for the project (e.g., "FreeManager", "MyApp")
    Must start with a letter and contain only letters and numbers.

.PARAMETER ModuleSelection
    What modules to keep or remove. Options:
    - 'remove:all'          - Remove all optional modules
    - 'keep:Tags'           - Keep only Tags module
    - 'keep:Appointments'   - Keep only Appointments module
    - 'keep:Invoices'       - Keep only Invoices module
    - 'keep:EmailTemplates' - Keep only EmailTemplates module
    - 'keep:Locations'      - Keep only Locations module
    - 'keep:Payments'       - Keep only Payments module
    - 'keep:Services'       - Keep only Services module

.PARAMETER OutputDirectory
    The directory where the renamed project will be placed.
    Will be created if it doesn't exist.

.PARAMETER Branch
    The branch to clone from (default: main)

.PARAMETER SkipClone
    Skip cloning and use existing FreeCRM source in current directory

.EXAMPLE
    .\Fork-FreeCRM.ps1 -NewName "FreeManager" -ModuleSelection "keep:Tags" -OutputDirectory "C:\Projects\FreeManager"

.EXAMPLE
    .\Fork-FreeCRM.ps1 -NewName "MyApp" -ModuleSelection "remove:all" -OutputDirectory ".\output"

.EXAMPLE
    .\Fork-FreeCRM.ps1 -NewName "FreeGLBA" -ModuleSelection "keep:Tags" -OutputDirectory "D:\FreeGLBA" -Branch "main"

.NOTES
    Author: FreeManager Tools
    Requires: Windows (for the .exe tools)
    Source: https://github.com/WSU-EIT/FreeCRM
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "New project name (letters and numbers only, must start with letter)")]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string]$NewName,

    [Parameter(Mandatory = $true, HelpMessage = "Module selection (e.g., 'keep:Tags', 'remove:all')")]
    [ValidatePattern('^(keep|remove):(Tags|Appointments|Invoices|EmailTemplates|Locations|Payments|Services|all)$')]
    [string]$ModuleSelection,

    [Parameter(Mandatory = $true, HelpMessage = "Output directory for the forked project")]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $false, HelpMessage = "Branch to clone (default: main)")]
    [string]$Branch = "main",

    [Parameter(Mandatory = $false, HelpMessage = "Skip cloning, use current directory")]
    [switch]$SkipClone
)

# Configuration
$RepoUrl = "https://github.com/WSU-EIT/FreeCRM.git"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RemoveExe = Join-Path $ScriptDir "Remove Modules from FreeCRM.exe"
$RenameExe = Join-Path $ScriptDir "Rename FreeCRM.exe"

# Colors for output
function Write-Step($msg) { Write-Host "`n[STEP] $msg" -ForegroundColor Cyan }
function Write-Success($msg) { Write-Host "[OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "[WARN] $msg" -ForegroundColor Yellow }
function Write-Err($msg) { Write-Host "[ERROR] $msg" -ForegroundColor Red }

# Banner
Write-Host ""
Write-Host "=======================================" -ForegroundColor Magenta
Write-Host "  FreeCRM Fork Tool" -ForegroundColor Magenta
Write-Host "=======================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "  New Name:         $NewName" -ForegroundColor White
Write-Host "  Module Selection: $ModuleSelection" -ForegroundColor White
Write-Host "  Output Directory: $OutputDirectory" -ForegroundColor White
Write-Host "  Branch:           $Branch" -ForegroundColor White
Write-Host ""

# Validate executables exist
Write-Step "Validating tools..."
if (-not (Test-Path $RemoveExe)) {
    Write-Err "Remove Modules tool not found: $RemoveExe"
    Write-Host "Please ensure the .exe files are in the same directory as this script."
    exit 1
}
if (-not (Test-Path $RenameExe)) {
    Write-Err "Rename tool not found: $RenameExe"
    Write-Host "Please ensure the .exe files are in the same directory as this script."
    exit 1
}
Write-Success "Tools found"

# Create temp directory for cloning
$TempDir = Join-Path $env:TEMP "FreeCRM-Fork-$(Get-Random)"

try {
    if (-not $SkipClone) {
        # Clone the repository
        Write-Step "Cloning FreeCRM repository..."
        New-Item -ItemType Directory -Force -Path $TempDir | Out-Null

        $cloneArgs = @("clone", "--depth", "1", "--branch", $Branch, $RepoUrl, $TempDir)
        $result = & git @cloneArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Err "Failed to clone repository"
            Write-Host $result
            exit 1
        }
        Write-Success "Repository cloned to $TempDir"

        $WorkDir = $TempDir
    } else {
        Write-Step "Using current directory (skip clone mode)"
        $WorkDir = Get-Location
    }

    # Change to working directory
    Push-Location $WorkDir

    # Run module removal
    Write-Step "Running module removal: $ModuleSelection"
    & $RemoveExe $ModuleSelection
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Module removal failed (exit code: $LASTEXITCODE)"
        Pop-Location
        exit 1
    }
    Write-Success "Module removal complete"

    # Run rename
    Write-Step "Renaming project to: $NewName"
    & $RenameExe $NewName
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Rename failed (exit code: $LASTEXITCODE)"
        Pop-Location
        exit 1
    }
    Write-Success "Rename complete"

    Pop-Location

    # Prepare output directory
    Write-Step "Preparing output directory..."
    $OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

    if (Test-Path $OutputDirectory) {
        Write-Warn "Output directory exists. Contents will be replaced."
        # Remove existing contents except .git
        Get-ChildItem -Path $OutputDirectory -Force |
            Where-Object { $_.Name -ne ".git" } |
            Remove-Item -Recurse -Force
    } else {
        New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    }

    # Copy files (excluding .git and .github)
    Write-Step "Copying files to output directory..."
    $excludeList = @(".git", ".github", "artifacts")

    Get-ChildItem -Path $WorkDir -Force |
        Where-Object { $_.Name -notin $excludeList } |
        ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $OutputDirectory -Recurse -Force
        }

    Write-Success "Files copied to $OutputDirectory"

    # Summary
    Write-Host ""
    Write-Host "=======================================" -ForegroundColor Green
    Write-Host "  Fork Complete!" -ForegroundColor Green
    Write-Host "=======================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Project Name: $NewName" -ForegroundColor White
    Write-Host "  Location:     $OutputDirectory" -ForegroundColor White
    Write-Host ""
    Write-Host "  Next Steps:" -ForegroundColor Yellow
    Write-Host "    1. cd `"$OutputDirectory`"" -ForegroundColor Gray
    Write-Host "    2. dotnet restore" -ForegroundColor Gray
    Write-Host "    3. dotnet build" -ForegroundColor Gray
    Write-Host ""

} finally {
    # Cleanup temp directory
    if (-not $SkipClone -and (Test-Path $TempDir)) {
        Write-Step "Cleaning up temp files..."
        Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Success "Cleanup complete"
    }
}
