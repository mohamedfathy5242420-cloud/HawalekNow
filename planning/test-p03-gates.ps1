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
$closedBaseline = (Get-Content (Join-Path $fixture 'docs/CURRENT_STATE.md') -Raw) -match '(?m)^- Status: Done\r?$'
$closedFiles = @{}
if ($closedBaseline) {
    # Preserve the actual accepted state, then run all original cases against the
    # captured pre-acceptance state. Neither history nor closure may bypass checks.
    foreach ($path in @('docs/CURRENT_STATE.md', 'planning/ROADMAP.md', 'planning/phase-03/PHASE_REVIEW.md', 'planning/phase-03/handoffs/S07-to-P04.md') + @(1..7 | ForEach-Object { 'planning/phase-03/steps/S{0:d2}.md' -f $_ })) {
        $closedFiles[$path] = [IO.File]::ReadAllText((Join-Path $fixture $path))
    }
    [IO.File]::Copy((Join-Path $fixture 'docs/P03_REVIEWED_STATE.md'), (Join-Path $fixture 'docs/CURRENT_STATE.md'), $true)
    foreach ($path in @('planning/phase-03/PHASE_REVIEW.md', 'planning/phase-03/handoffs/S07-to-P04.md') + @(1..7 | ForEach-Object { 'planning/phase-03/steps/S{0:d2}.md' -f $_ })) {
        $text = $closedFiles[$path]
        $text = [regex]::Replace($text, '(?m)^Status: Done\r?$', 'Status: InReview')
        foreach ($name in @('UserApproval', 'ReviewStatus', 'V4')) { $text = [regex]::Replace($text, "(?m)^${name}: Approved\r?`$", "${name}: Pending") }
        [IO.File]::WriteAllText((Join-Path $fixture $path), $text)
    }
    $legacyMap = [regex]::Replace($closedFiles['planning/ROADMAP.md'], '(?ms)^### P03-S0[1-7] .*?(?=^### |\z)', { param($m) $m.Value.Replace('Status: Done؛', 'Status: InReview؛') })
    [IO.File]::WriteAllText((Join-Path $fixture 'planning/ROADMAP.md'), $legacyMap)
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
if ($closedBaseline) {
    foreach ($path in $closedFiles.Keys) { [IO.File]::WriteAllText((Join-Path $fixture $path), $closedFiles[$path]) }
    Check-P03
    Reject-P03 'closure acceptance absent' 'docs/DECISIONS.md' { param($t) $t.Replace('## D020 ', '## D099 ') }
    Reject-P03 'closure merge not authorized' 'docs/DECISIONS.md' { param($t) $t -replace '(?m)^MergeAuthorization: Approved\r?$', 'MergeAuthorization: NotAuthorized' }
    Reject-P03 'closure different reviewed head' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- ReviewedHead: d6e9f7e4d65c891a4a932cccd02c88fe00c35057', '- ReviewedHead: 0000000000000000000000000000000000000000') }
    Reject-P03 'closure approval missing' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- UserApproval: Approved', '- UserApproval: Pending') }
    Reject-P03 'manual Postman falsely passed' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- UserPostmanReview: Deferred', '- UserPostmanReview: Passed') }
    Reject-P03 'teaching falsely passed' 'planning/phase-03/PHASE_REVIEW.md' { param($t) $t.Replace('EducationalWalkthrough: Deferred', 'EducationalWalkthrough: Passed') }
    Reject-P03 'closure step not approved' 'planning/phase-03/steps/S02.md' { param($t) $t.Replace('UserApproval: Approved', 'UserApproval: Pending') }
    Reject-P03 'closure skipped tests' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.tests.skipped=1; $e|ConvertTo-Json -Depth 30 }
    Reject-P03 'fresh closure tests failed' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.closure.tests.failed=1; $e|ConvertTo-Json -Depth 30 }
    Reject-P03 'Docker admitted forbidden input' 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.closure.docker.forbidden=@('appsettings.Local.json'); $e|ConvertTo-Json -Depth 30 }
    Reject-P03 'handoff acceptance missing' 'planning/phase-03/handoffs/S07-to-P04.md' { param($t) $t.Replace('UserApproval: Approved', 'UserApproval: Pending') }
    foreach ($checkName in @('ClosureLockedRestore', 'ClosureReleaseBuild', 'ClosureTests', 'ClosureFormat', 'ClosureDocker')) {
        $script:removedClosureCheck = $checkName
        Reject-P03 "missing $checkName" 'planning/phase-03/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.checks=@($e.checks|Where-Object name -ne $script:removedClosureCheck); $e|ConvertTo-Json -Depth 30 }
    }
    Reject-P03 'premature Ready before merge gate' 'planning/phase-03/handoffs/S07-to-P04.md' { param($t) $t.Replace('Status: Draft', 'Status: Ready') }
    Reject-P03 'unverified post merge claimed' 'planning/phase-03/handoffs/S07-to-P04.md' { param($t) $t.Replace('TransitionGate: Pending', 'TransitionGate: Passed') }
    Check-P03
}
Write-Output "PASS: actual P03 baseline accepted; $script:p03Rejections invalid predecessor/source/approval/phase claims rejected."
