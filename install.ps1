# Installs SSMS Shared Queries into the SSMS 22 PER-MACHINE Extensions folder and refreshes
# the shell configuration. The first install self-elevates (UAC) to write into the SSMS
# program folder; it then grants the current user Modify there so later updates need no admin.
#
# Modes:
#   .\install.ps1                # install the locally built VSIX if present, else the latest release
#   .\install.ps1 -FromRelease   # always download and install the latest GitHub release
#
# One-command install (no clone or local build needed) - run in PowerShell:
#   irm https://raw.githubusercontent.com/rbritta/ssms-shared-queries/main/install.ps1 -OutFile "$env:TEMP\sq.ps1"; & "$env:TEMP\sq.ps1"
#
# WHY per-machine: SSMS 22 only merges the pkgdef of extensions installed under the product
# folder (<SSMS>\Common7\IDE\Extensions\). Extensions dropped in the per-user folder
# (%LocalAppData%\Microsoft\SSMS\<ver>\Extensions\) are listed in the catalog but their pkgdef
# is NEVER merged -> the package never registers (no Options page, no menu, never loads).
param([switch]$FromRelease, [switch]$Elevated)
$ErrorActionPreference = "Stop"

$RepoSlug  = "rbritta/ssms-shared-queries"
$AssetName = "SsmsSharedQueries.vsix"

# An open SSMS holds the extension DLL, so it must be closed before we can replace it. Check
# here, in the USER's own console, so the message is visible instead of flashing by in the
# elevated window. (Re-checked after elevation too, in case it was reopened in between.)
if (Get-Process -Name "Ssms" -ErrorAction SilentlyContinue) {
    Write-Host "SSMS is running. Close all SSMS windows first, then run this again." -ForegroundColor Yellow
    return
}

# --- self-elevate ------------------------------------------------------------
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Elevating (admin required to write into the SSMS program folder)..."
    $argList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"", "-Elevated")
    if ($FromRelease) { $argList += "-FromRelease" }
    Start-Process powershell -Verb RunAs -ArgumentList $argList -Wait
    return
}

# From here on we run elevated (a separate window when launched via UAC). Wrap everything so a
# failure - or the final "Done" - stays on screen instead of vanishing when the window closes.
try {
    # Re-check in the elevated context: SSMS may have been reopened since the check above.
    if (Get-Process -Name "Ssms" -ErrorAction SilentlyContinue) {
        throw "SSMS is running. Close it first, then re-run the installer."
    }

    # --- pick the VSIX: locally built, or downloaded from the latest release -----
    $root = Split-Path -Parent $MyInvocation.MyCommand.Path
    $localVsix = Join-Path $root "src\SsmsSharedQueries\bin\Release\$AssetName"
    if (-not $FromRelease -and (Test-Path $localVsix)) {
        $vsix = $localVsix
        Write-Host "Installing locally built VSIX: $vsix"
    }
    else {
        $vsix = Join-Path $env:TEMP $AssetName
        $url = "https://github.com/$RepoSlug/releases/latest/download/$AssetName"
        Write-Host "Downloading the latest release: $url"
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $url -OutFile $vsix -UseBasicParsing
    }

    # --- locate the SSMS 22 install ---------------------------------------------
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $ssmsRoot = $null
    if (Test-Path $vswhere) {
        $ssmsRoot = & $vswhere -all -prerelease -products Microsoft.VisualStudio.Product.Ssms -property installationPath | Select-Object -First 1
    }
    if (-not $ssmsRoot) { $ssmsRoot = "C:\Program Files\Microsoft SQL Server Management Studio 22\Release" }
    if (-not (Test-Path $ssmsRoot)) { throw "SSMS 22 install not found ($ssmsRoot)." }
    $ssmsExe = Join-Path $ssmsRoot "Common7\IDE\SSMS.exe"

    # --- extract the vsix to a staging dir --------------------------------------
    $stage = Join-Path $env:TEMP "SsmsSharedQueries_stage"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $zip = Join-Path $env:TEMP "SsmsSharedQueries_install.zip"
    Copy-Item $vsix $zip -Force
    Expand-Archive -Path $zip -DestinationPath $stage -Force
    Remove-Item $zip -Force

    # --- target: a STABLE folder name. Per-machine discovery is by folder scan, so the
    #     name is arbitrary; the VSIX build's hashed folder name changes on every build, so we
    #     don't use it. Remove any older copies of THIS extension first (match by Identity in
    #     their manifest) to avoid duplicates.
    $extDir = Join-Path $ssmsRoot "Common7\IDE\Extensions"
    Get-ChildItem $extDir -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $vm = Join-Path $_.FullName "extension.vsixmanifest"
        if ((Test-Path $vm) -and ((Get-Content $vm -Raw) -match "SsmsSharedQueries\.")) {
            Remove-Item $_.FullName -Recurse -Force
        }
    }

    $dest = Join-Path $extDir "SsmsSharedQueries"
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Copy-Item -Path (Join-Path $stage '*') -Destination $dest -Recurse -Force
    Remove-Item $stage -Recurse -Force

    # --- grant the current user Modify so FUTURE updates need no elevation -------
    $user = "$env:USERDOMAIN\$env:USERNAME"
    & icacls $dest /grant "$($user):(OI)(CI)M" /T | Out-Null
    Write-Host "Granted Modify on $dest to $user (future updates won't need admin)."

    # --- refresh the shell config so the pkgdef gets merged ---------------------
    Write-Host "Installed to: $dest"
    Write-Host "Refreshing SSMS configuration (/updateconfiguration)..."
    Start-Process $ssmsExe -ArgumentList "/updateconfiguration" -Wait

    Write-Host "Done. Start SSMS, then open: Tools > SSMS Shared Queries" -ForegroundColor Green
}
catch {
    Write-Host "Install failed: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    # Keep this window (usually the elevated one) open so the result above is readable.
    if ($Elevated) { try { Read-Host "Press Enter to close" | Out-Null } catch { } }
}
