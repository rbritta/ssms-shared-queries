# Builds the SSMS Shared Queries VSIX using the desktop MSBuild from VS Build Tools 2022.
# Prereq (one-time, admin):
#   winget install --id Microsoft.VisualStudio.2022.BuildTools -e --override "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --includeRecommended"
$ErrorActionPreference = "Stop"
$root   = Split-Path -Parent $MyInvocation.MyCommand.Path
$csproj = Join-Path $root "src\SsmsSharedQueries\SsmsSharedQueries.csproj"

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) { throw "vswhere not found. Install VS Build Tools 2022." }

$msbuild = & $vswhere -products * -requires Microsoft.Component.MSBuild -latest -find "MSBuild\**\Bin\amd64\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) { $msbuild = & $vswhere -products * -requires Microsoft.Component.MSBuild -latest -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1 }
if (-not $msbuild) { throw "Desktop MSBuild not found. Install VS Build Tools 2022 (ManagedDesktopBuildTools workload)." }

Write-Host "MSBuild: $msbuild"
& $msbuild $csproj /t:Restore /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }
& $msbuild $csproj /p:Configuration=Release /p:DeployExtension=false /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$vsix = Join-Path $root "src\SsmsSharedQueries\bin\Release\SsmsSharedQueries.vsix"
Write-Host "`nVSIX: $vsix"
