param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$fixture = Join-Path $Root ('tmp/p02-gates-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
$paths = @(& git -c "safe.directory=$([IO.Path]::GetFullPath($Root).Replace('\','/'))" -C $Root ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate P02 gate fixture.' }
foreach ($path in $paths | Sort-Object -Unique) {
    if ($path -eq 'docs/TARGET_SCHEDULE.md') { continue }
    $destination = Join-Path $fixture $path
    [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
    [IO.File]::Copy((Join-Path $Root $path), $destination)
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
Reject-P02 'P01 approval missing' 'planning/phase-01/CLOSED_STATE.md' { param($t) $t.Replace('- UserApproval: Approved', '- UserApproval: Pending') }
Reject-P02 'P01 phase review not approved' 'planning/phase-01/PHASE_REVIEW.md' { param($t) $t.Replace('ReviewStatus: Approved', 'ReviewStatus: Pending') }
Reject-P02 'P01 handoff not Ready' 'planning/phase-01/handoffs/S08-to-P02.md' { param($t) $t -replace '(?m)^Status: Ready', 'Status: Draft' }
Reject-P02 'execution not authorized' 'docs/DECISIONS.md' { param($t) $t.Replace('AuthorizedPhase: P02', 'AuthorizedPhase: P03') }
Reject-P02 'phase falsely Done' 'docs/CURRENT_STATE.md' { param($t) $t -replace '(?m)^- Status: [^\r\n]+', '- Status: Done' }
Reject-P02 'phase falsely approved' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- UserApproval: Pending', '- UserApproval: Approved') }
Reject-P02 'V4 falsely complete' 'planning/phase-02/PHASE_REVIEW.md' { param($t) $t.Replace('V4: Pending', 'V4: Approved') }
Reject-P02 'step falsely approved' 'planning/phase-02/steps/S01.md' { param($t) $t.Replace('UserApproval: Pending', 'UserApproval: Approved') }
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
Write-Output "PASS: actual P02 baseline accepted; $script:p02Rejections unauthorized phase/approval/evidence mutations rejected."
