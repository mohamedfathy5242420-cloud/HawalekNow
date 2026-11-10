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
        $mutated = & $Mutation $original
        if ($mutated -eq $original) { throw "Mutation made no change: $Label" }
        Write-Fixture $Path $mutated
        $rejected = $false
        try { Check-Fixture } catch { $rejected = $true }
        if (-not $rejected) { throw "Invalid phase state accepted: $Label" }
        $script:rejections++
    } finally { Write-Fixture $Path $original }
}
$recordPaths = @('docs/CURRENT_STATE.md', 'planning/phase-01/steps/S07.md', 'planning/phase-01/steps/S08.md', 'planning/phase-01/PHASE_REVIEW.md', 'planning/phase-01/handoffs/S07-to-S08.md', 'planning/phase-01/handoffs/S08-to-P02.md', 'planning/ROADMAP.md', 'planning/phase-01/verification.json', 'docs/DECISIONS.md', 'planning/phase-01/CLOSURE.md')
$actual = @{}
foreach ($path in $recordPaths) { if (Test-Path (Join-Path $fixture $path)) { $actual[$path] = Read-Fixture $path } }
$closedBaseline = $actual['docs/CURRENT_STATE.md'] -match '(?m)^- Status: Done\r?$'
Check-Fixture
if ($closedBaseline) {
    foreach ($path in $recordPaths | Where-Object { $_ -notin @('docs/DECISIONS.md', 'planning/phase-01/CLOSURE.md', 'planning/phase-01/verification.json', 'planning/ROADMAP.md') }) {
        $text = $actual[$path]
        $text = $text -replace '(?m)^(- )?Status: (Done|Ready)\r?$', '${1}Status: InReview'
        $text = $text -replace '(?m)^(- )?(UserApproval|ReviewStatus|V4): Approved\r?$', '${1}${2}: Pending'
        $text = $text -replace '(?m)^(- )?(UserScalarReview|UserPostmanReview|EducationalWalkthrough): Deferred\r?$', '${1}${2}: Pending'
        if ($path -like '*S07-to-S08.md') { $text = $text -replace '(?m)^Status: InReview', 'Status: InternalReady' }
        if ($path -like '*S08-to-P02.md') { $text = $text -replace '(?m)^Status: InReview', 'Status: Draft' }
        Write-Fixture $path $text
    }
    $text = $actual['planning/ROADMAP.md']
    foreach ($id in @('P01-S07', 'P01-S08')) {
        $block = [regex]::Match($text, '(?ms)^### ' + $id + ' .*?(?=^### |\z)').Value
        $text = $text.Replace($block, ($block -replace '(?m)^- Status: Done؛', '- Status: InReview؛'))
    }
    Write-Fixture 'planning/ROADMAP.md' $text
    Check-Fixture
}
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
Reject 'roadmap disagrees with the step state' 'planning/ROADMAP.md' {
    param($t)
    $block = [regex]::Match($t, '(?ms)^### P01-S08 .*?(?=^### |\z)').Value
    $t.Replace($block, ($block -replace '(?m)^- Status: [^؛\r\n]+', '- Status: Done'))
}
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
$fixtureMap = Read-Fixture 'planning/ROADMAP.md'
$s08Block = [regex]::Match($fixtureMap, '(?ms)^### P01-S08 .*?(?=^### |\z)').Value
if (-not $s08Block) { throw 'Cannot find S08 for the synthetic review baseline.' }
$reviewBlock = $s08Block -replace '(?m)^- Status: [^؛\r\n]+', '- Status: InReview'
$reviewBlock = $reviewBlock.Replace('ExecutionStatus: InProgress', 'ExecutionStatus: Verified').Replace('VerificationStatus: Failed', 'VerificationStatus: Passed')
Write-Fixture 'planning/ROADMAP.md' ($fixtureMap.Replace($s08Block, $reviewBlock))
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
# Restore actual closed records and exercise the separate human-approved closure branch.
if ($closedBaseline) {
    foreach ($path in $actual.Keys) { Write-Fixture $path $actual[$path] }
    Check-Fixture
    Reject 'closed without user approval' $statePath { param($t) $t -replace '- UserApproval: Approved', '- UserApproval: Pending' }
    Reject 'closed without phase review approval' $reviewPath { param($t) $t -replace 'ReviewStatus: Approved', 'ReviewStatus: Pending' }
    Reject 'closed with falsified Scalar result' $reviewPath { param($t) $t -replace 'UserScalarReview: Deferred', 'UserScalarReview: Passed' }
    Reject 'closed with falsified Postman result' $reviewPath { param($t) $t -replace 'UserPostmanReview: Deferred', 'UserPostmanReview: Passed' }
    Reject 'closed with falsified teaching completion' $reviewPath { param($t) $t -replace 'EducationalWalkthrough: Deferred', 'EducationalWalkthrough: Passed' }
    Reject 'closed without deferred state marker' $statePath { param($t) $t -replace '- EducationalWalkthrough: Deferred', '- EducationalWalkthrough: Pending' }
    Reject 'old approval date' $reviewPath { param($t) $t -replace 'ApprovalDate: 2026-11-10', 'ApprovalDate: 2026-11-09' }
    Reject 'different accepted technical head' $statePath { param($t) $t -replace '- ReviewedHead: [a-f0-9]{40}', ('- ReviewedHead: ' + ('b' * 40)) }
    Reject 'missing D013 authorization' 'docs/DECISIONS.md' { param($t) $t -replace 'ClosureAuthorization: Approved', 'ClosureAuthorization: Pending' }
    Reject 'missing D013 merge authorization' 'docs/DECISIONS.md' { param($t) $t -replace 'MergeAuthorization: Approved', 'MergeAuthorization: Pending' }
    Reject 'fake D013 manual completion' 'docs/DECISIONS.md' { param($t) $t -replace 'EducationalWalkthrough: Deferred', 'EducationalWalkthrough: Passed' }
    Reject 'closed step still in review' $stepPath { param($t) $t -replace '(?m)^Status: Done', 'Status: InReview' }
    Reject 'closed with unverified step' $stepPath { param($t) $t -replace 'ExecutionStatus: Verified', 'ExecutionStatus: InProgress' }
    Reject 'closed without Ready handoff' 'planning/phase-01/handoffs/S08-to-P02.md' { param($t) $t -replace '(?m)^Status: Ready', 'Status: Draft' }
    Reject 'handoff invents manual success' 'planning/phase-01/handoffs/S08-to-P02.md' { param($t) $t -replace 'UserPostmanReview: Deferred', 'UserPostmanReview: Passed' }
    Reject 'next phase marked started' $statePath { param($t) $t -replace '- P02Status: NotStarted', '- P02Status: InProgress' }
    Reject 'next phase roadmap marked started' 'planning/ROADMAP.md' { param($t) $t -replace '- Status: NotStarted؛ Handoff P01 Ready', '- Status: InProgress؛ Handoff P01 Ready' }
    Reject 'closed with failed CI' $evidencePath { param($t) $e = $t | ConvertFrom-Json; $e.ci.conclusion = 'failure'; $e | ConvertTo-Json -Depth 12 }
    Reject 'closed with failed format' $evidencePath { param($t) $e = $t | ConvertFrom-Json; $c = $e.checks | Where-Object name -eq 'Format'; $c.status = 'Failed'; $c.exitCode = 2; $e | ConvertTo-Json -Depth 12 }
    Reject 'closed with skipped tests' $evidencePath { param($t) $e = $t | ConvertFrom-Json; $e.tests.skipped = 1; $e | ConvertTo-Json -Depth 12 }
    Reject 'closed roadmap differs' 'planning/ROADMAP.md' { param($t) $t -replace '- Status: Done؛ ExecutionStatus: Verified', '- Status: InReview؛ ExecutionStatus: Verified' }
    Write-Fixture 'planning/phase-02/steps/S01.md' '# Synthetic unauthorized P02 fixture after P01 approval'
    $rejected = $false
    try { Check-Fixture } catch { $rejected = $true }
    if (-not $rejected) { throw 'P01 approval incorrectly authorized P02 execution.' }
    $script:rejections++
    Remove-Item -LiteralPath $futureFile
    Check-Fixture
}
Write-Output "PASS: actual state, unapproved review and approved closure baselines accepted; $script:rejections invalid transitions/evidence claims rejected. Original 24 review rejections retained; source files and user schedule untouched."
