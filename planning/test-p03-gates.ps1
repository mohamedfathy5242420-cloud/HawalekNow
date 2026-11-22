param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$fixture = Join-Path $Root ('tmp/p03-gates-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
$paths = @(& git -c "safe.directory=$([IO.Path]::GetFullPath($Root).Replace('\','/'))" -C $Root ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate P03 fixture.' }
foreach ($path in $paths | Sort-Object -Unique) {
    if ($path -eq 'docs/TARGET_SCHEDULE.md') { continue }
    $destination = Join-Path $fixture $path
    [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
    [IO.File]::Copy((Join-Path $Root $path), $destination)
}
function Check-P03 { & (Join-Path $fixture 'planning/verify-roadmap.ps1') -Root $fixture | Out-Null }
$script:p03Rejections = 0
function Reject-P03($label, $path, [scriptblock]$mutation) {
    $destination = Join-Path $fixture $path
    $original = [IO.File]::ReadAllText($destination)
    try {
        $changed = & $mutation $original
        if ($changed -eq $original) { throw "No P03 mutation: $label" }
        [IO.File]::WriteAllText($destination, $changed)
        $rejected = $false
        try { Check-P03 } catch { $rejected = $true }
        if (-not $rejected) { throw "Invalid P03 state accepted: $label" }
        $script:p03Rejections++
    } finally { [IO.File]::WriteAllText($destination, $original) }
}
Check-P03
Reject-P03 'authorization absent' 'docs/DECISIONS.md' { param($t) $t.Replace('AuthorizedPhase: P03', 'AuthorizedPhase: P04') }
Reject-P03 'source decision absent' 'docs/DECISIONS.md' { param($t) $t.Replace('## D019 ', '## D099 ') }
Reject-P03 'complete Egypt falsely claimed' 'docs/DECISIONS.md' { param($t) $t.Replace('Completeness: NotClaimed', 'Completeness: Complete') }
Reject-P03 'P02 approval missing' 'docs/P02_CLOSED_STATE.md' { param($t) $t.Replace('- UserApproval: Approved', '- UserApproval: Pending') }
Reject-P03 'P02 not merged' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.predecessor.merged=$false; $e|ConvertTo-Json -Depth 30 }
Reject-P03 'source evidence changed' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.source.commit='0000000000000000000000000000000000000000'; $e|ConvertTo-Json -Depth 30 }
Reject-P03 'source coverage falsely complete' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.source.completeEgyptClaimed=$true; $e|ConvertTo-Json -Depth 30 }
Reject-P03 'post merge checks missing' 'planning/phase-02/MERGE_RECEIPT.md' { param($t) $t.Replace('PostMergeVerification: Passed', 'PostMergeVerification: Pending') }
Reject-P03 'P03 falsely Done' 'docs/CURRENT_STATE.md' { param($t) $t -replace '(?m)^- Status: [^\r\n]+', '- Status: Done' }
Reject-P03 'P03 falsely approved' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- UserApproval: Pending', '- UserApproval: Approved') }
Reject-P03 'step V4 falsely approved' 'planning/phase-03/steps/S01.md' { param($t) $t.Replace('V4: Pending', 'V4: Approved') }
Reject-P03 'duplicate step field' 'planning/phase-03/steps/S01.md' { param($t) $t + "`nStatus: InReview`n" }
Reject-P03 'wrong active file' 'docs/CURRENT_STATE.md' { param($t) $t -replace '(?m)^- ActiveStepFile: [^\r\n]+', '- ActiveStepFile: planning/phase-04/steps/S01.md' }
Reject-P03 'P04 state started' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- P04Status: NotStarted', '- P04Status: InProgress') }
if ((Get-Content (Join-Path $fixture 'docs/CURRENT_STATE.md') -Raw) -match '(?m)^- Status: InReview\r?$') {
    Reject-P03 'final review without full tests' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.checks=@($e.checks|Where-Object name -ne 'Tests'); $e|ConvertTo-Json -Depth 30 }
    Reject-P03 'final review with skipped tests' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.tests.skipped=1; $e|ConvertTo-Json -Depth 30 }
    Reject-P03 'final review with inconsistent count' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.tests.passed++; $e|ConvertTo-Json -Depth 30 }
    Reject-P03 'final review with failed format' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $c=$e.checks|Where-Object name -eq 'Format'; $c.status='Failed'; $c.exitCode=1; $e|ConvertTo-Json -Depth 30 }
    Reject-P03 'CI from wrong event' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.ci.event='workflow_dispatch'; $e|ConvertTo-Json -Depth 30 }
    Reject-P03 'CI still running' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.ci.status='in_progress'; $e|ConvertTo-Json -Depth 30 }
    Reject-P03 'CI missing exact head' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.ci.headSha='unknown'; $e|ConvertTo-Json -Depth 30 }
    Reject-P03 'one failed CI job' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.ci.jobs[0].conclusion='failure'; $e|ConvertTo-Json -Depth 30 }
    Reject-P03 'P04 handoff incorrectly Ready' 'planning/phase-03/handoffs/S07-to-P04.md' { param($t) $t.Replace('Status: Draft','Status: Ready') }
}
Check-P03
Write-Output "PASS: actual P03 baseline accepted; $script:p03Rejections invalid predecessor/source/approval/phase claims rejected."
