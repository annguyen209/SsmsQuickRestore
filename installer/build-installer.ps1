<#
.SYNOPSIS
    Build the SSMS Quick Restore Inno Setup installer.

.DESCRIPTION
    1. Builds the project in Release configuration.
    2. Patches the generated pkgdef so SSMS resolves the DLL via $PackageFolder$.
    3. Runs ISCC.exe to produce installer\Output\SsmsQuickRestore-Setup-<version>.exe.
#>

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'

$Root    = Split-Path $PSScriptRoot -Parent
$SrcCsproj = Join-Path $Root 'src\SsmsRestoreDrop.csproj'
$OutDir    = Join-Path $Root 'src\bin\Release\net48'
$Iss       = Join-Path $PSScriptRoot 'SsmsQuickRestore.iss'
$Iscc      = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'

if (-not (Test-Path $Iscc)) {
    throw "Inno Setup 6 not found at $Iscc. Install from https://jrsoftware.org/isdl.php"
}

Write-Host '[1/3] Building Release...' -ForegroundColor Cyan
& dotnet build $SrcCsproj -c Release --nologo -v m `
    -p:DeployExtension=false -p:DeployVsixExtensionFiles=false
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }

Write-Host '[2/3] Patching pkgdef (Assembly -> CodeBase)...' -ForegroundColor Cyan
$pkgdefPath = Join-Path $OutDir 'SsmsRestoreDrop.pkgdef'
$content    = [System.IO.File]::ReadAllText($pkgdefPath, [System.Text.Encoding]::Unicode)
$content    = $content -replace '"Assembly"="[^"]+"',
                                '"CodeBase"="$PackageFolder$\SsmsRestoreDrop.dll"'
[System.IO.File]::WriteAllText($pkgdefPath, $content, [System.Text.Encoding]::Unicode)

Write-Host '[3/3] Compiling installer with ISCC...' -ForegroundColor Cyan
& $Iscc $Iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed (exit $LASTEXITCODE)" }

Write-Host ''
$out = Get-ChildItem (Join-Path $PSScriptRoot 'Output') -Filter 'SsmsQuickRestore-Setup-*.exe' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "Done: $($out.FullName) ($([math]::Round($out.Length/1KB)) KB)" -ForegroundColor Green
