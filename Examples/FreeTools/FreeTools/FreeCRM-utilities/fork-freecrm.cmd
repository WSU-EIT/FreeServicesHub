@echo off
REM Fork-FreeCRM.cmd - Quick launcher for Fork-FreeCRM.ps1
REM Usage: fork-freecrm.cmd NewName ModuleSelection OutputDirectory
REM Example: fork-freecrm.cmd FreeManager "keep:Tags" C:\Projects\FreeManager

if "%~1"=="" (
    echo.
    echo Usage: fork-freecrm.cmd NewName ModuleSelection OutputDirectory
    echo.
    echo Examples:
    echo   fork-freecrm.cmd FreeManager "keep:Tags" C:\Projects\FreeManager
    echo   fork-freecrm.cmd MyApp "remove:all" .\output
    echo.
    echo Module Selections:
    echo   remove:all          - Remove all optional modules
    echo   keep:Tags           - Keep only Tags module
    echo   keep:Appointments   - Keep only Appointments module
    echo   keep:Invoices       - Keep only Invoices module
    echo   keep:EmailTemplates - Keep only EmailTemplates module
    echo   keep:Locations      - Keep only Locations module
    echo   keep:Payments       - Keep only Payments module
    echo   keep:Services       - Keep only Services module
    echo.
    exit /b 1
)

powershell -ExecutionPolicy Bypass -File "%~dp0Fork-FreeCRM.ps1" -NewName %1 -ModuleSelection %2 -OutputDirectory %3 %4 %5 %6
