# Nightly local docs audit. Cloud Cursor automations cannot see this folder
# (no git remote). Same reason GPU-10 is a scheduled task, not the cloud slot.
#
#   powershell -NoProfile -File .\Tools\run-docs-daily.ps1
#   powershell -NoProfile -File .\Tools\run-docs-daily.ps1 -SkipAgent
#
# Never deletes Baselines, Runs, or task archive.

param(
    [switch]$SkipAgent,
    [int]$AgentTimeoutSec = 1200,
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$runsDir = Join-Path $ProjectPath "Trials\Runs\docs_daily"
New-Item -ItemType Directory -Force -Path $runsDir | Out-Null

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$day = Get-Date -Format "yyyy-MM-dd"
$outJson = Join-Path $runsDir "$stamp.json"
$outMd = Join-Path $runsDir "$stamp.md"
$latestJson = Join-Path $ProjectPath "Trials\Runs\docs_daily_latest.json"
$latestMd = Join-Path $ProjectPath "Trials\Runs\docs_daily_latest.md"

function Get-Rel([string]$abs) {
    return $abs.Substring($ProjectPath.Length).TrimStart('\', '/')
}

function Test-Has([string]$path, [string]$needle) {
    if (-not (Test-Path -LiteralPath $path)) { return $false }
    $t = [System.IO.File]::ReadAllText($path)
    return $t.Contains($needle)
}

$findings = New-Object System.Collections.Generic.List[object]
function Add-Finding([string]$level, [string]$id, [string]$detail) {
    $script:findings.Add([ordered]@{ level = $level; id = $id; detail = $detail })
}

# --- presence ---
$mustExist = @(
    "DOCS.md",
    "CURRENT.md",
    "AGENTS.md",
    "README.md",
    ".cursor\ROSTER.md",
    ".cursor\agents\docs.md",
    ".cursor\rules\docs-map.mdc",
    ".cursor\rules\docs-english.mdc",
    ".cursor\rules\current-front.mdc",
    ".cursor\rules\trial-protocol.mdc",
    ".cursor\tasks\OPEN.md",
    ".cursor\tasks\DOCS-DAILY.md",
    "Trials\README.md",
    "Trials\Baselines\README.md",
    "Trials\Baselines\stable-pd\README.md",
    "Trials\Baselines\stable-pd\s_on.json",
    "Trials\Baselines\current\README.md",
    "Trials\Baselines\legacy-no-provenance\README.md",
    "Trials\Baselines\pelvis-world\README.md",
    "Trials\Baselines\knee-flipped\README.md",
    "Trials\Runs\README.md",
    "Tools\README.md",
    "Tools\docs-daily-prompt.txt"
)

$missing = @()
foreach ($rel in $mustExist) {
    $abs = Join-Path $ProjectPath $rel
    if (-not (Test-Path -LiteralPath $abs)) {
        $missing += $rel
        Add-Finding "error" "missing" $rel
    }
}

# --- pointer canon ---
$pointerFiles = @(
    "README.md",
    "CURRENT.md",
    "DOCS.md",
    "Trials\README.md",
    "Trials\Baselines\README.md",
    ".cursor\rules\docs-map.mdc"
)
foreach ($rel in $pointerFiles) {
    $abs = Join-Path $ProjectPath $rel
    if (-not (Test-Path -LiteralPath $abs)) { continue }
    if (-not (Test-Has $abs "stable-pd")) {
        Add-Finding "error" "no_stable_pd_pointer" $rel
    }
}

foreach ($rel in @("README.md", "Trials\README.md")) {
    $abs = Join-Path $ProjectPath $rel
    if (-not (Test-Path -LiteralPath $abs)) { continue }
    $t = [System.IO.File]::ReadAllText($abs)
    if ($t.Contains("Baselines/current") -and -not $t.Contains("stable-pd")) {
        Add-Finding "error" "stale_canon_pointer" $rel
    }
}

$legacy = Join-Path $ProjectPath "Trials\Baselines\legacy-no-provenance\README.md"
if (Test-Path -LiteralPath $legacy) {
    $t = [System.IO.File]::ReadAllText($legacy)
    if ($t.Contains("Baselines/current") -and -not $t.Contains("stable-pd")) {
        Add-Finding "error" "legacy_points_at_current" "Trials/Baselines/legacy-no-provenance/README.md"
    }
}

$currReadme = Join-Path $ProjectPath "Trials\Baselines\current\README.md"
if (Test-Path -LiteralPath $currReadme) {
    $t = [System.IO.File]::ReadAllText($currReadme)
    if (-not $t.Contains("stable-pd")) {
        Add-Finding "warn" "current_readme_unlabeled" "Trials/Baselines/current/README.md"
    }
}

# --- frozen folders still there ---
$baselineDirs = @(
    "Trials\Baselines\stable-pd",
    "Trials\Baselines\pelvis-world",
    "Trials\Baselines\knee-flipped",
    "Trials\Baselines\current",
    "Trials\Baselines\legacy-no-provenance"
)
foreach ($rel in $baselineDirs) {
    $abs = Join-Path $ProjectPath $rel
    if (-not (Test-Path -LiteralPath $abs)) {
        Add-Finding "error" "baseline_folder_gone" $rel
    }
}

$extLogs = "E:\Games\Unity3D LOGS\trials"
$extOk = Test-Path -LiteralPath $extLogs
if (-not $extOk) {
    Add-Finding "warn" "external_csv_missing" $extLogs
}

# living docs must be English (Cyrillic leftover)
$cyr = [regex]'[\u0400-\u04FF]'
$scan = @()
$cursorDir = Join-Path $ProjectPath ".cursor"
if (Test-Path -LiteralPath $cursorDir) {
    $scan += Get-ChildItem -LiteralPath $cursorDir -Recurse -File | Where-Object { $_.Extension -in '.md', '.mdc' }
}
$scan += Get-ChildItem -LiteralPath $ProjectPath -File | Where-Object { $_.Extension -eq '.md' }
$trialsDir = Join-Path $ProjectPath "Trials"
if (Test-Path -LiteralPath $trialsDir) {
    $scan += Get-ChildItem -LiteralPath $trialsDir -Recurse -File | Where-Object { $_.Extension -eq '.md' }
}
$toolsDir = Join-Path $ProjectPath "Tools"
if (Test-Path -LiteralPath $toolsDir) {
    $scan += Get-ChildItem -LiteralPath $toolsDir -File | Where-Object { $_.Extension -in '.md', '.txt' }
}
$seen = @{}
foreach ($f in $scan) {
    if ($seen.ContainsKey($f.FullName)) { continue }
    $seen[$f.FullName] = $true
    if ($f.FullName -match '\\obj\\|\\bin\\') { continue }
    if ($f.Extension -ne '.md' -and $f.Extension -ne '.mdc' -and $f.Extension -ne '.txt') { continue }
    $text = [System.IO.File]::ReadAllText($f.FullName)
    if ($cyr.IsMatch($text)) {
        Add-Finding "error" "cyrillic_in_docs" (Get-Rel $f.FullName)
    }
}

# handSize contradiction (report only)
$humanCs = Join-Path $ProjectPath "Assets\Human.cs"
$visHand = Join-Path $ProjectPath ".cursor\tasks\VIS-HAND.md"
$handCode = $null
$handCard = $null
if (Test-Path -LiteralPath $humanCs) {
    $hc = [System.IO.File]::ReadAllText($humanCs)
    if ($hc -match 'handSize\s*=\s*new Vector2\(([\d.]+)f,\s*([\d.]+)f\)') {
        $handCode = $Matches[2]
    }
}
if (Test-Path -LiteralPath $visHand) {
    $ht = [System.IO.File]::ReadAllText($visHand)
    if ($ht -match '\*\*0\.19\*\*') { $handCard = "0.19" }
    elseif ($ht -match '\*\*0\.32\*\*') { $handCard = "0.32" }
}
if ($handCode -and $handCard -and $handCode -ne $handCard) {
    Add-Finding "warn" "handsize_mismatch" ("code=" + $handCode + " card=" + $handCard)
}

$errItems = @($findings | Where-Object { $_.level -eq "error" })
$warnItems = @($findings | Where-Object { $_.level -eq "warn" })
$verdict = if ($errItems.Count -gt 0) { "docs_broken" }
elseif ($warnItems.Count -gt 0) { "docs_drift" }
else { "docs_ok" }

# --- optional local agent (never the cloud VM) ---
$agent = [ordered]@{
    attempted = $false
    skipped = [bool]$SkipAgent
    launcher = $null
    exitCode = $null
    note = $null
}

if (-not $SkipAgent) {
    $prompt = "Follow Tools/docs-daily-prompt.txt and .cursor/tasks/DOCS-DAILY.md. You are the local document manager. Do not delete Baselines or Runs."
    $cursorCmd = $null
    $cursorArgs = $null

    $agentCli = Get-Command "agent" -ErrorAction SilentlyContinue
    $cursorAgentCli = Get-Command "cursor-agent" -ErrorAction SilentlyContinue

    if ($agentCli) {
        $cursorCmd = $agentCli.Source
        $cursorArgs = @("-p", "--trust", $prompt)
        $agent.launcher = "agent -p"
    }
    elseif ($cursorAgentCli) {
        $cursorCmd = $cursorAgentCli.Source
        $cursorArgs = @("-p", "--trust", $prompt)
        $agent.launcher = "cursor-agent -p"
    }
    else {
        $agent.note = "No headless agent CLI (agent / cursor-agent). Mechanical audit ran. Do not launch cursor.exe at 02:00 (that opens the IDE). Cloud automation cannot see this folder."
    }

    if ($cursorCmd) {
        $agent.attempted = $true
        $agentLog = Join-Path $runsDir "$stamp.agent.log"
        try {
            $p = Start-Process -FilePath $cursorCmd -ArgumentList $cursorArgs -WorkingDirectory $ProjectPath `
                -NoNewWindow -PassThru -RedirectStandardOutput $agentLog -RedirectStandardError "$agentLog.err"
            if (-not $p.WaitForExit($AgentTimeoutSec * 1000)) {
                try { $p.Kill() } catch { }
                $agent.note = "Local agent timed out after $AgentTimeoutSec s. log=$agentLog"
                $agent.exitCode = -1
            }
            else {
                $agent.exitCode = $p.ExitCode
                $agent.note = "Local agent finished. log=$agentLog"
            }
        }
        catch {
            $agent.note = "Local agent failed to start: $($_.Exception.Message)"
        }
    }
}

$report = @{
    capturedLocal = (Get-Date).ToString("o")
    day = $day
    card = "DOCS-DAILY"
    verdict = $verdict
    errorCount = $errItems.Count
    warnCount = $warnItems.Count
    missingFiles = $missing
    findings = @($findings.ToArray())
    externalCsvPresent = $extOk
    handSize = @{ codeY = $handCode; cardY = $handCard }
    agent = $agent
    note = "Cloud Cursor Automations cannot check this project (no git checkout). Do not delete Baselines or Runs."
}

$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($outJson, ($report | ConvertTo-Json -Depth 8), $utf8)

$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine("# Docs daily $day")
[void]$md.AppendLine("")
[void]$md.AppendLine("- verdict: **$verdict**")
[void]$md.AppendLine("- errors: $($errItems.Count)")
[void]$md.AppendLine("- warns: $($warnItems.Count)")
[void]$md.AppendLine("- external CSV folder: $extOk")
[void]$md.AppendLine("- agent: $($agent.launcher) attempted=$($agent.attempted) skip=$($agent.skipped)")
if ($agent.note) { [void]$md.AppendLine("- agent note: $($agent.note)") }
[void]$md.AppendLine("")
[void]$md.AppendLine("## Findings")
if ($findings.Count -eq 0) {
    [void]$md.AppendLine("None. Pointers name stable-pd. Required files are present.")
}
else {
    foreach ($f in $findings) {
        [void]$md.AppendLine("- **$($f.level)** ``$($f.id)``: $($f.detail)")
    }
}
[void]$md.AppendLine("")
[void]$md.AppendLine("Do not delete Trials/Baselines or Trials/Runs.")
[System.IO.File]::WriteAllText($outMd, $md.ToString(), $utf8)

Copy-Item -LiteralPath $outJson -Destination $latestJson -Force
Copy-Item -LiteralPath $outMd -Destination $latestMd -Force

Write-Host "DOCS-DAILY $verdict errors=$($errItems.Count) warns=$($warnItems.Count) -> $outJson"

if ($errItems.Count -gt 0) { exit 1 }
exit 0
