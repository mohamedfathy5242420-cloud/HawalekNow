param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$fixture = Join-Path $Root ('tmp/p02-gates-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
$paths = @(& git -c "safe.directory=$([IO.Path]::GetFullPath($Root).Replace('\','/'))" -C $Root ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate P02 gate fixture.' }
foreach ($path in $paths | Sort-Object -Unique) {
    if ($path -eq 'docs/TARGET_SCHEDULE.md') { continue }
    if ($path.StartsWith('planning/phase-03/') -or $path.StartsWith('planning/phase-04/')) { continue }
    $destination = Join-Path $fixture $path
    [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
    [IO.File]::Copy((Join-Path $Root $path), $destination)
}
if ((Get-Content -LiteralPath (Join-Path $fixture 'docs/CURRENT_STATE.md') -Raw) -match '(?m)^- Phase: P0[34]\r?$') {
    [IO.File]::Copy((Join-Path $fixture 'docs/P02_CLOSED_STATE.md'), (Join-Path $fixture 'docs/CURRENT_STATE.md'), $true)
    $mapPath = Join-Path $fixture 'planning/ROADMAP.md'
    $legacyMap = [IO.File]::ReadAllText($mapPath)
    $legacyMap = [regex]::Replace($legacyMap, '(?ms)(^### P0[34]-S0[1-7] .*?)(?=^### |\z)', { param($m) [regex]::Replace($m.Value, '(?m)^- Status: [^\r\n]+\r?\n', '') })
    [IO.File]::WriteAllText($mapPath, $legacyMap)
    # The historical P02 baseline intentionally contains no P03 execution files.
    # Preserve current documentation in the real worktree; remove only its forward links inside this fixture.
    foreach ($legacyDoc in (Get-ChildItem -LiteralPath $fixture -Filter '*.md' -Recurse -File)) {
        $legacyText = [IO.File]::ReadAllText($legacyDoc.FullName)
        $legacyText = [regex]::Replace($legacyText, '\[([^\]\r\n]+)\]\([^)\r\n]*phase-0[34][^)\r\n]*\)', '$1')
        [IO.File]::WriteAllText($legacyDoc.FullName, $legacyText)
    }
}
function Check-P02Fixture { & (Join-Path $fixture 'planning/verify-roadmap.ps1') -Root $fixture | Out-Null }
function Reject-P02([string]$Label, [string]$Path, [scriptblock]$Mutation) {
    $destination = Join-Path $fixture $Path
    $original = [IO.File]::ReadAllText($destination)
    try {
        $changed = & $Mutation $original
        if ($changed -eq $original) { throw "No P02 mutation: $Label" }
        [IO.File]::WriteAllText($destination, $changed)
        $rejected = $false
        try { Check-P02Fixture } catch { $rejected = $true }
        if (-not $rejected) { throw "Invalid P02 transition accepted: $Label" }
        $script:p02Rejections++
    } finally { [IO.File]::WriteAllText($destination, $original) }
}
Check-P02Fixture
$script:p02Rejections = 0
$closedP02 = (Get-Content -LiteralPath (Join-Path $fixture 'docs/CURRENT_STATE.md') -Raw) -match '(?m)^- Status: Done\r?$'
Reject-P02 'P01 approval missing' 'planning/phase-01/CLOSED_STATE.md' { param($t) $t.Replace('- UserApproval: Approved', '- UserApproval: Pending') }
Reject-P02 'P01 phase review not approved' 'planning/phase-01/PHASE_REVIEW.md' { param($t) $t.Replace('ReviewStatus: Approved', 'ReviewStatus: Pending') }
Reject-P02 'P01 handoff not Ready' 'planning/phase-01/handoffs/S08-to-P02.md' { param($t) $t -replace '(?m)^Status: Ready', 'Status: Draft' }
Reject-P02 'execution not authorized' 'docs/DECISIONS.md' { param($t) $t.Replace('AuthorizedPhase: P02', 'AuthorizedPhase: P03') }
Reject-P02 'phase status conflicts with approval' 'docs/CURRENT_STATE.md' { param($t) $t -replace '(?m)^- Status: [^\r\n]+', $(if ($closedP02) { '- Status: InReview' } else { '- Status: Done' }) }
Reject-P02 'phase approval conflicts with status' 'docs/CURRENT_STATE.md' { param($t) if ($closedP02) { $t.Replace('- UserApproval: Approved', '- UserApproval: Pending') } else { $t.Replace('- UserApproval: Pending', '- UserApproval: Approved') } }
Reject-P02 'V4 conflicts with acceptance' 'planning/phase-02/PHASE_REVIEW.md' { param($t) if ($closedP02) { $t.Replace('V4: Approved', 'V4: Pending') } else { $t.Replace('V4: Pending', 'V4: Approved') } }
Reject-P02 'step approval conflicts with phase' 'planning/phase-02/steps/S01.md' { param($t) if ($closedP02) { $t.Replace('UserApproval: Approved', 'UserApproval: Pending') } else { $t.Replace('UserApproval: Pending', 'UserApproval: Approved') } }
Reject-P02 'duplicate step status' 'planning/phase-02/steps/S01.md' { param($t) $t + "`nStatus: InReview`n" }
Reject-P02 'wrong active file' 'docs/CURRENT_STATE.md' { param($t) $t -replace '(?m)^- ActiveStepFile: [^\r\n]+', '- ActiveStepFile: planning/phase-03/steps/S01.md' }
Reject-P02 'P03 state starts' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- P03Status: NotStarted', '- P03Status: InProgress') }
Reject-P02 'PR8 not merged' 'planning/phase-02/verification.json' { param($t) $e = $t | ConvertFrom-Json; $e.predecessor.merged = $false; $e | ConvertTo-Json -Depth 15 }
Reject-P02 'unapproved next phase selected' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- Phase: P02', '- Phase: P03') }
$future = Join-Path $fixture 'planning/phase-03/steps/S01.md'
[IO.Directory]::CreateDirectory((Split-Path $future -Parent)) | Out-Null
try {
    [IO.File]::WriteAllText($future, '# Synthetic unauthorized P03')
    $rejected = $false
    try { Check-P02Fixture } catch { $rejected = $true }
    if (-not $rejected) { throw 'P03 execution file incorrectly accepted.' }
    $script:p02Rejections++
} finally { Remove-Item -LiteralPath $future }
Check-P02Fixture
if ($closedP02) {
    Reject-P02 'acceptance decision absent' 'docs/DECISIONS.md' { param($t) $t.Replace('## D017 ', '## D999 ') }
    Reject-P02 'merge not authorized' 'docs/DECISIONS.md' { param($t) $t.Replace('MergeAuthorization: Approved', 'MergeAuthorization: NotAuthorized') }
    Reject-P02 'wrong acceptance date' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- ApprovalDate: 2026-11-21', '- ApprovalDate: 2026-11-10') }
    Reject-P02 'wrong reviewed head' 'planning/phase-02/steps/S09.md' { param($t) $t.Replace('ReviewedHead: d08e057e11516b25dca7fb22403a39a12b5b976b', 'ReviewedHead: 0000000000000000000000000000000000000000') }
    Reject-P02 'personal review falsely Passed' 'planning/phase-02/PHASE_REVIEW.md' { param($t) $t.Replace('UserPostmanReview: Deferred', 'UserPostmanReview: Passed') }
    Reject-P02 'teaching falsely Passed' 'docs/DECISIONS.md' { param($t) $t.Replace('EducationalWalkthrough: Deferred', 'EducationalWalkthrough: Passed') }
    Reject-P02 'handoff not Ready' 'planning/phase-02/handoffs/S09-to-P03.md' { param($t) $t -replace '(?m)^Status: Ready', 'Status: Draft' }
    foreach ($checkName in @('ClosureLockedRestore', 'ClosureReleaseBuild', 'ClosureTests', 'ClosureFormat')) {
        Reject-P02 "missing $checkName evidence" 'planning/phase-02/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.checks=@($e.checks|Where-Object name -ne $checkName); $e|ConvertTo-Json -Depth 30 }
    }
    Reject-P02 'step closure incomplete' 'planning/phase-02/steps/S09.md' { param($t) $t -replace '(?m)^Status: Done', 'Status: InReview' }
    Reject-P02 'closure tests failed' 'planning/phase-02/verification.json' { param($t) $e=$t|ConvertFrom-Json; $check=$e.checks|Where-Object name -eq 'ClosureTests'; $check.status='Failed';$check.exitCode=1; $e|ConvertTo-Json -Depth 30 }
}
Write-Output "PASS: actual P02 baseline accepted; $script:p02Rejections unauthorized phase/approval/evidence mutations rejected."
