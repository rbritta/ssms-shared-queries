# The notes of one release: its section of CHANGELOG.md (Keep a Changelog).
#
#   .\tools\release-notes.ps1 -Tag v1.2.3 -OutFile release-notes.md
#
# release.yml runs it on a `v*` tag. It exits 1 with the reason when the tag does not name the version in
# source.extension.vsixmanifest, or when CHANGELOG.md has no section (or an empty one) for that version.
# -Changelog and -Version read another file or version (the tests use them). Without -OutFile the notes go to stdout.
param(
    [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Tag,
    [string]$Changelog = "",
    [string]$Version = "",
    [string]$OutFile = ""
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

function Fail([string]$message) {
    [Console]::Error.WriteLine("release-notes: $message")
    exit 1
}

if (-not $Version) {
    $manifest = [xml](Get-Content -Raw -Encoding UTF8 (Join-Path $root "src\SsmsSharedQueries\source.extension.vsixmanifest"))
    $Version = $manifest.PackageManifest.Metadata.Identity.Version
}
if (-not $Changelog) { $Changelog = Join-Path $root "CHANGELOG.md" }

$named = ($Tag -replace '^refs/tags/', '') -replace '^v', ''
if ($named -ne $Version) {
    $shown = if ($Tag) { $Tag } else { "(none)" }
    Fail "the tag $shown does not name the version in source.extension.vsixmanifest, $Version"
}

$lines = ((Get-Content -Raw -Encoding UTF8 $Changelog) -replace "`r`n?", "`n") -split "`n"
$head = '^## \[' + [regex]::Escape($Version) + '\](\s|$)'
$start = -1
for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match $head) { $start = $i; break } }
$body = ""
if ($start -ge 0) {
    $end = $lines.Count
    for ($i = $start + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^## ' -or $lines[$i] -match '^\[[^\]]+\]:\s') { $end = $i; break }
    }
    if ($end -gt $start + 1) { $body = ($lines[($start + 1)..($end - 1)] -join "`n").Trim() }
}
if (-not $body) { Fail "CHANGELOG.md has no section, or an empty one, for $Version" }

if ($OutFile) {
    [IO.File]::WriteAllText($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutFile), $body + "`n", (New-Object Text.UTF8Encoding($false)))
}
else {
    [Console]::Out.Write($body + "`n")
}
exit 0
