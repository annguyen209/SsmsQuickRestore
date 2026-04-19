#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Build and deploy SSMS Quick Restore directly to the SSMS Extensions folder.
.DESCRIPTION
    1. Builds the project in Release configuration.
    2. Patches SsmsRestoreDrop.pkgdef to use CodeBase (required by SSMS — not the VS user hive).
    3. Copies DLL + pkgdef + resources to the SSMS Extensions folder.
    4. Runs Ssms.exe /updateconfiguration so SSMS re-scans the Extensions folder.
#>

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'

$SsmsDir     = 'C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE'
$ExtDest     = "$SsmsDir\Extensions\SsmsQuickRestore"
$SsmsExe     = "$SsmsDir\Ssms.exe"
$SrcDir      = "$PSScriptRoot\src"
$OutDir      = "$SrcDir\bin\Release\net48"

# ── 1. Build ────────────────────────────────────────────────────────────────
Write-Host "Building..." -ForegroundColor Cyan
& dotnet build "$SrcDir\SsmsRestoreDrop.csproj" -c Release --nologo -v m
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)" }

# ── 2. Patch pkgdef ─────────────────────────────────────────────────────────
Write-Host "Patching pkgdef..." -ForegroundColor Cyan
$pkgdefPath = "$OutDir\SsmsRestoreDrop.pkgdef"
$content    = [System.IO.File]::ReadAllText($pkgdefPath, [System.Text.Encoding]::Unicode)
$content    = $content -replace '"Assembly"="[^"]+"',
                                '"CodeBase"="$PackageFolder$\SsmsRestoreDrop.dll"'
[System.IO.File]::WriteAllText($pkgdefPath, $content, [System.Text.Encoding]::Unicode)

# ── 3. Deploy ───────────────────────────────────────────────────────────────
Write-Host "Deploying to $ExtDest..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $ExtDest | Out-Null

Copy-Item "$OutDir\SsmsRestoreDrop.dll"       $ExtDest -Force
Copy-Item "$OutDir\SsmsRestoreDrop.pkgdef"    $ExtDest -Force
Copy-Item "$OutDir\extension.vsixmanifest"    $ExtDest -Force
Copy-Item "$OutDir\Resources"                 $ExtDest -Recurse -Force

# ── 4. Update SSMS configuration ────────────────────────────────────────────
Write-Host "Running /updateconfiguration..." -ForegroundColor Cyan
$p = Start-Process $SsmsExe -ArgumentList '/updateconfiguration' -Wait -PassThru -NoNewWindow
if ($p.ExitCode -ne 0) { Write-Warning "updateconfiguration exited $($p.ExitCode)" }

Write-Host "Done. Launch SSMS to verify the extension loads." -ForegroundColor Green
