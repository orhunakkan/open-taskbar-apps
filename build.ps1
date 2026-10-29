# Builds build\OpenTaskbar.exe and the installer dist\OpenTaskbarSetup.exe.
# Needs only what ships with Windows (the .NET Framework 4.x C# compiler) plus Inno Setup 6 for the installer.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Test-Path assets\app.ico)) { & .\tools\make-icon.ps1 }

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
New-Item -ItemType Directory -Force build | Out-Null
& $csc /nologo /target:winexe /optimize+ /out:build\OpenTaskbar.exe `
    /win32icon:assets\app.ico /win32manifest:src\app.manifest `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll src\*.cs
if ($LASTEXITCODE -ne 0) { throw "Compile failed" }
Write-Host "Built build\OpenTaskbar.exe"

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { Write-Warning "Inno Setup 6 not found; skipping installer."; return }
& $iscc /Q installer\OpenTaskbar.iss
if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }
Write-Host "Built dist\OpenTaskbarSetup.exe"
