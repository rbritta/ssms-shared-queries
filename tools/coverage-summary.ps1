# Prints the line coverage of the production source files (src/) from a Cobertura report, one row per file and a
# total. CI runs it after `dotnet test --collect "Code Coverage;Format=cobertura"`; the tests link the pure source
# files into the test assembly, so the report holds both and only src/ is counted.
#
#   .\tools\coverage-summary.ps1 -Path TestResults
param([Parameter(Mandatory = $true)][string]$Path)
$ErrorActionPreference = "Stop"

$file = if (Test-Path $Path -PathType Container) {
    Get-ChildItem $Path -Recurse -Filter *.cobertura.xml | Sort-Object LastWriteTime | Select-Object -Last 1
} else { Get-Item $Path }
if (-not $file) { throw "no *.cobertura.xml under $Path" }

[xml]$report = Get-Content -Raw -Encoding UTF8 $file.FullName
$rows = @{}
foreach ($class in $report.coverage.packages.package.classes.class) {
    $name = ([string]$class.filename).Replace([char]92, [char]47)
    if ($name -notmatch '/src/(.+)$') { continue }
    $key = $Matches[1]
    if (-not $rows.ContainsKey($key)) { $rows[$key] = @{ Covered = @{}; Valid = @{} } }
    foreach ($line in $class.SelectNodes('.//line')) {
        $rows[$key].Valid[$line.number] = $true
        if ([int]$line.hits -gt 0) { $rows[$key].Covered[$line.number] = $true }
    }
}
if (-not $rows.Count) { throw "the report has no file under src/" }

$covered = 0; $valid = 0
$inv = [Globalization.CultureInfo]::InvariantCulture
$row = { param($name, $c, $v) [string]::Format($inv, "{0,-46} {1,9} {2,6:N1}%", $name, "$c/$v", (100.0 * $c / [Math]::Max($v, 1))) }
[string]::Format($inv, "{0,-46} {1,9} {2,7}", "file (src/)", "lines", "cover")
foreach ($key in ($rows.Keys | Sort-Object)) {
    $c = $rows[$key].Covered.Count; $v = $rows[$key].Valid.Count
    $covered += $c; $valid += $v
    & $row $key $c $v
}
& $row "total" $covered $valid
