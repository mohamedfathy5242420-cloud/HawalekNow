param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$fixture = Join-Path $Root ('tmp/phase-gates-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
$paths = @(& git -c "safe.directory=$([IO.Path]::GetFullPath($Root).Replace('\','/'))" -C $Root ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate fixture source files.' }
foreach ($path in $paths | Sort-Object -Unique) {
    if ($path -eq 'docs/TARGET_SCHEDULE.md') { continue }
    $destination = Join-Path $fixture $path
    [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
    [IO.File]::Copy((Join-Path $Root $path), $destination)
}
$utf8 = [Text.UTF8Encoding]::new($false)
function Write-Fixture([string]$Path, [string]$Text) {
    $destination = Join-Path $fixture $Path
    [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
    [IO.File]::WriteAllText($destination, $Text, $utf8)
}
function Read-Fixture([string]$Path) { [IO.File]::ReadAllText((Join-Path $fixture $Path)) }
function Check-Fixture { & (Join-Path $fixture 'planning/verify-roadmap.ps1') -Root $fixture | Out-Null }
function Reject([string]$Label, [string]$Path, [scriptblock]$Mutation) {
    $original = Read-Fixture $Path
    try {
        Write-Fixture $Path (& $Mutation $original)
        $rejected = $false
        try { Check-Fixture } catch { $rejected = $true }
        if (-not $rejected) { throw "Invalid phase state accepted: $Label" }
        $script:rejections++
    } finally { Write-Fixture $Path $original }
}
Check-Fixture
$script:rejections = 0
$statePath = 'docs/CURRENT_STATE.md'
$stepPath = 'planning/phase-01/steps/S08.md'
$reviewPath = 'planning/phase-01/PHASE_REVIEW.md'
$evidencePath = 'planning/phase-01/verification.json'
Reject 'phase Done before approval' $statePath { param($t) $t -replace '(?m)^- Status: .+$', '- Status: Done' }
Reject 'phase approval inferred' $statePath { param($t) $t -replace '- UserApproval: Pending', '- UserApproval: Approved' }
Reject 'human V4 inferred' $reviewPath { param($t) $t -replace 'V4: Pending', 'V4: Passed' }
Reject 'manual Scalar review inferred' $reviewPath { param($t) $t -replace 'UserScalarReview: Pending', 'UserScalarReview: Approved' }
Reject 'next phase starts' $statePath { param($t) $t -replace '- Phase: P01', '- Phase: P02' -replace '- Step: P01-S08', '- Step: P02-S01' }
Reject 'wrong active file' $statePath { param($t) $t -replace '- ActiveStepFile: [^\r\n]+', '- ActiveStepFile: planning/phase-02/steps/S01.md' }
Reject 'Ready next-phase handoff' 'planning/phase-01/handoffs/S08-to-P02.md' { param($t) $t -replace 'Status: Draft', 'Status: Ready' }
Reject 'internal progress without predecessor verification' 'planning/phase-01/steps/S07.md' { param($t) $t -replace 'ExecutionStatus: Verified', 'ExecutionStatus: InProgress' }
Reject 'internal progress without internal handoff' 'planning/phase-01/handoffs/S07-to-S08.md' { param($t) $t -replace 'Status: InternalReady', 'Status: Draft' }
Reject 'invented step approval' $stepPath { param($t) $t -replace 'UserApproval: Pending', 'UserApproval: Approved' }
Reject 'invented step Done' $stepPath { param($t) $t -replace '(?m)^Status: [^\r\n]+', 'Status: Done' }
Reject 'duplicate status field' $stepPath { param($t) $t + "`nStatus: InReview`n" }
Reject 'historical approval removed' 'planning/phase-01/steps/S06.md' { param($t) $t -replace 'UserApproval: Approved', 'UserApproval: Pending' }
Reject 'Passed with failed command' $evidencePath {
    param($t) $e = $t | ConvertFrom-Json; $e.checks[0].exitCode = 1; $e | ConvertTo-Json -Depth 12
}
Reject 'Passed without running command' $evidencePath {
    param($t) $e = $t | ConvertFrom-Json; $e.checks[0].exitCode = $null; $e | ConvertTo-Json -Depth 12
}
Reject 'failed predecessor check' $evidencePath {
    param($t) $e = $t | ConvertFrom-Json; $e.checks[0].status = 'Failed'; $e.checks[0].exitCode = 1; $e | ConvertTo-Json -Depth 12
}
# Test InReview/CI gate using explicitly synthetic data ONLY in the task fixture.
$originalState = Read-Fixture $statePath
$originalStep = Read-Fixture $stepPath
$originalReview = Read-Fixture $reviewPath
$originalEvidence = Read-Fixture $evidencePath
Write-Fixture $statePath ($originalState -replace '(?m)^- Status: [^\r\n]+', '- Status: InReview')
Write-Fixture $stepPath ($originalStep -replace '(?m)^Status: [^\r\n]+', 'Status: InReview' -replace '(?m)^ExecutionStatus: [^\r\n]+', 'ExecutionStatus: Verified' -replace '(?m)^VerificationStatus: [^\r\n]+', 'VerificationStatus: Passed')
Write-Fixture $reviewPath ($originalReview -replace '(?m)^Status: [^\r\n]+', 'Status: InReview')
Write-Fixture 'planning/ROADMAP.md' ((Read-Fixture 'planning/ROADMAP.md') -replace '(?m)^- Status: InProgress؛ \[ملف التنفيذ\]\(phase-01/steps/S08.md\)', '- Status: InReview؛ [ملف التنفيذ](phase-01/steps/S08.md)')
$e = $originalEvidence | ConvertFrom-Json
foreach ($check in $e.checks) { $check.status = 'Passed'; $check.exitCode = 0; $check.command = 'SYNTHETIC FIXTURE ONLY' }
$e.ci = [pscustomobject]@{event = 'pull_request'; conclusion = 'success'; runId = 1; headSha = ('a' * 40); url = 'https://github.com/mohamedfathy5242420-cloud/HawalekNow/actions/runs/1'; jobs = @('Documentation and Git rules', 'Restore, build, tests and format', 'API and SQL image builds') | ForEach-Object { @{name = $_; conclusion = 'success'} } }
Write-Fixture $evidencePath ($e | ConvertTo-Json -Depth 12)
Check-Fixture
Reject 'CI failed but verification claims Passed' $evidencePath { param($t) $e = $t | ConvertFrom-Json; $e.ci.conclusion = 'failure'; $e | ConvertTo-Json -Depth 12 }
Reject 'CI job failed but verification claims Passed' $evidencePath { param($t) $e = $t | ConvertFrom-Json; $e.ci.jobs[1].conclusion = 'failure'; $e | ConvertTo-Json -Depth 12 }
Reject 'manual run substituted for PR run' $evidencePath { param($t) $e = $t | ConvertFrom-Json; $e.ci.event = 'workflow_dispatch'; $e | ConvertTo-Json -Depth 12 }
Reject 'local format failed but S08 Verified' $evidencePath { param($t) $e = $t | ConvertFrom-Json; $c = $e.checks | Where-Object name -eq 'Format'; $c.status = 'Failed'; $c.exitCode = 2; $e | ConvertTo-Json -Depth 12 }
Reject 'tests contain failures' $evidencePath { param($t) $e = $t | ConvertFrom-Json; $e.tests.failed = 1; $e | ConvertTo-Json -Depth 12 }
Reject 'pending CI treated as success' $evidencePath { param($t) $e = $t | ConvertFrom-Json; $c = $e.checks | Where-Object name -eq 'CI'; $c.status = 'Pending'; $c.exitCode = $null; $e | ConvertTo-Json -Depth 12 }
$futureFile = Join-Path $fixture 'planning/phase-02/steps/S01.md'
Write-Fixture 'planning/phase-02/steps/S01.md' '# Synthetic unauthorized P02 fixture'
$rejected = $false
try { Check-Fixture } catch { $rejected = $true }
if (-not $rejected) { throw 'P02 execution artifact accepted before authorization.' }
$script:rejections++
Remove-Item -LiteralPath $futureFile
Check-Fixture
Write-Output "PASS: actual state and synthetic internal/review baselines accepted; $script:rejections invalid transitions/evidence claims rejected. Fixtures retained under tmp; source files and user schedule untouched."
