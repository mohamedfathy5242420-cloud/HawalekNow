param([string]$Root = (Split-Path $PSScriptRoot -Parent), [string]$StatePath = 'docs/CURRENT_STATE.md', [switch]$AllowAuthorizedP03)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Read-P02([string]$Path) { Get-Content -LiteralPath (Join-Path $Root $Path) -Raw }
function P02-Field([string]$Text, [string]$Name, [string]$Prefix = '') {
    $values = [regex]::Matches($Text, '(?m)^' + [regex]::Escape($Prefix + $Name) + ': ([^\r\n]+)\r?$')
    if ($values.Count -ne 1) { throw "P02 expected exactly one field: $Name" }
    $values[0].Groups[1].Value
}
function P02-Expect([string]$Text, [string]$Name, [string]$Value, [string]$Prefix = '') {
    if ((P02-Field $Text $Name $Prefix) -ne $Value) { throw "P02 invalid field: $Name; expected $Value" }
}
function P02-Require([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
$state = Read-P02 $StatePath
P02-Expect $state 'Phase' 'P02' '- '
P02-Expect $state 'ExecutionAuthorization' 'D015' '- '
P02-Expect $state 'P03Status' 'NotStarted' '- '
P02-Expect $state 'ExecutionMode' 'Phase' '- '
$phaseStatus = P02-Field $state 'Status' '- '
$closed = $phaseStatus -eq 'Done'
$approval = if ($closed) { 'Approved' } else { 'Pending' }
foreach ($field in @('UserApproval', 'ReviewStatus', 'V4')) { P02-Expect $state $field $approval '- ' }
function P02-ClosureFields([string]$Text, [string]$Prefix = '') {
    foreach ($pair in @(@('AcceptanceDecision', 'D017'), @('ApprovalDate', '2026-11-21'), @('ReviewedHead', 'd08e057e11516b25dca7fb22403a39a12b5b976b'), @('AcceptanceBasis', 'CodeTestsCI'))) { P02-Expect $Text $pair[0] $pair[1] $Prefix }
    foreach ($field in @('UserScalarReview', 'UserPostmanReview', 'EducationalWalkthrough')) { P02-Expect $Text $field 'Deferred' $Prefix }
}
if ($closed) {
    P02-ClosureFields $state '- '
    $decisions = Read-P02 'docs/DECISIONS.md'
    $acceptance = [regex]::Match($decisions, '(?ms)^## D017 .*?(?=^## |\z)').Value
    foreach ($pair in @(@('Status', 'Approved'), @('ClosureAuthorization', 'Approved'), @('MergeAuthorization', 'Approved'), @('ApprovalDate', '2026-11-21'), @('ReviewedHead', 'd08e057e11516b25dca7fb22403a39a12b5b976b'), @('AcceptanceBasis', 'CodeTestsCI'), @('P03Execution', 'ConditionalOnP02Closure'))) { P02-Expect $acceptance $pair[0] $pair[1] }
    foreach ($field in @('UserScalarReview', 'UserPostmanReview', 'EducationalWalkthrough')) { P02-Expect $acceptance $field 'Deferred' }
}
$current = P02-Field $state 'Step' '- '
P02-Require ($current -match '^P02-S0[1-9]$') 'Only P02 S01 through S09 are authorized.'
$number = [int]$current.Substring(5)
P02-Require ($phaseStatus -in @('InProgress', 'InReview', 'Done')) 'Unsupported P02 state.'
P02-Expect $state 'ActiveStepFile' ("planning/phase-02/steps/S{0:d2}.md" -f $number) '- '
$evidence = Read-P02 'planning/phase-02/verification.json' | ConvertFrom-Json
P02-Require ($evidence.phase -eq 'P02' -and $evidence.predecessor.merged -eq $true -and $evidence.predecessor.pr -eq 8 -and $evidence.predecessor.approval -eq 'D013') 'P02 requires the approved, merged predecessor.'
P02-Require ($evidence.baseMain -eq 'b7a502051ac52f06b57c541e24f4bdde270a3392') 'Unexpected P02 base main.'
$checks = @{}
foreach ($check in $evidence.checks) {
    P02-Require (-not $checks.ContainsKey($check.name)) 'Duplicate P02 check.'
    P02-Require ($check.status -in @('Passed', 'Failed', 'Unavailable', 'Pending')) 'Invalid check status.'
    if ($check.status -eq 'Passed') { P02-Require ($null -ne $check.exitCode -and $check.exitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($check.command)) 'Passed requires an actual successful command.' }
    if ($check.status -eq 'Failed') { P02-Require ($null -ne $check.exitCode -and $check.exitCode -ne 0) 'Failed requires its failure exit code.' }
    $checks[$check.name] = $check
}
$template = Read-P02 'planning/templates/STEP_TEMPLATE.md'
$map = Read-P02 'planning/ROADMAP.md'
foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $Root 'planning/phase-02/steps') -File)) {
    P02-Require ($file.Name -match '^S0[1-9]\.md$') 'Unrecognized P02 execution file.'
}
foreach ($index in 1..9) {
    $id = 'S{0:d2}' -f $index
    $path = "planning/phase-02/steps/$id.md"
    if ($index -gt $number) { P02-Require (-not (Test-Path (Join-Path $Root $path))) 'A future P02 step started before its predecessor.'; continue }
    $step = Read-P02 $path
    foreach ($pair in @(@('Phase', 'P02'), @('ExecutionMode', 'Phase'), @('UserApproval', $approval), @('ReviewStatus', $approval), @('V4', $approval))) { P02-Expect $step $pair[0] $pair[1] }
    if ($closed) { P02-ClosureFields $step }
    $previous = if ($index -eq 1) { 'P01-S08' } else { 'P02-S{0:d2}' -f ($index - 1) }
    $next = if ($index -eq 9) { 'PhaseReview' } else { 'P02-S{0:d2}' -f ($index + 1) }
    P02-Expect $step 'PreviousStep' $previous
    P02-Expect $step 'NextStep' $next
    $execution = P02-Field $step 'ExecutionStatus'
    $verification = P02-Field $step 'VerificationStatus'
    $status = P02-Field $step 'Status'
    $allowedStatuses = if ($closed) { @('Done') } else { @('InProgress', 'InReview') }
    P02-Require ($status -in $allowedStatuses -and $execution -in @('InProgress', 'Verified') -and $verification -in @('Passed', 'Failed', 'Pending', 'Unavailable')) 'Invalid P02 step state.'
    P02-Require (($execution -eq 'Verified') -eq ($verification -eq 'Passed')) 'Verified requires Passed verification.'
    foreach ($heading in [regex]::Matches($template, '(?m)^## .+$')) { P02-Require ($step.Contains($heading.Value.Trim())) "Missing step heading: $id" }
    if ($index -lt $number -or $execution -eq 'Verified') {
        P02-Expect $step 'ExecutionStatus' 'Verified'
        P02-Expect $step 'Status' $(if ($closed) { 'Done' } else { 'InReview' })
        foreach ($kind in @('Build', 'Tests', 'Sql')) { $name = "$id$kind"; P02-Require ($checks.ContainsKey($name) -and $checks[$name].status -eq 'Passed') "Missing technical evidence: $name" }
        if ($index -lt 9) {
            $handoff = Read-P02 ("planning/phase-02/handoffs/$id-to-S{0:d2}.md" -f ($index + 1))
            foreach ($pair in @(@('From', "P02-$id"), @('To', $next), @('Status', 'InternalReady'), @('UserApproval', 'Pending'), @('ReviewStatus', 'Pending'))) { P02-Expect $handoff $pair[0] $pair[1] }
        }
    }
    $block = [regex]::Match($map, '(?ms)^### P02-' + $id + ' .*?(?=^### |\z)').Value
    P02-Require ($block -match ('(?m)^- Status: ' + $status + '؛')) "Roadmap disagrees with P02-$id."
}
$review = Read-P02 'planning/phase-02/PHASE_REVIEW.md'
P02-Expect $review 'Phase' 'P02'
P02-Expect $review 'Status' $phaseStatus
foreach ($field in @('UserApproval', 'ReviewStatus', 'V4')) { P02-Expect $review $field $approval }
if ($closed) { P02-ClosureFields $review }
$nextHandoffPath = Join-Path $Root 'planning/phase-02/handoffs/S09-to-P03.md'
if (Test-Path $nextHandoffPath) {
    $handoff = Read-P02 'planning/phase-02/handoffs/S09-to-P03.md'
    foreach ($pair in @(@('From', 'P02-S09'), @('To', 'P03-S01'), @('Status', $(if ($closed) { 'Ready' } else { 'Draft' })), @('UserApproval', $approval), @('ReviewStatus', $approval))) { P02-Expect $handoff $pair[0] $pair[1] }
    if ($closed) { P02-ClosureFields $handoff }
}
$future = Join-Path $Root 'planning/phase-03'
if ($AllowAuthorizedP03) {
    P02-Require $closed 'P03 requires closed P02.'
    $authorization = [regex]::Match((Read-P02 'docs/DECISIONS.md'), '(?ms)^## D018 .*?(?=^## |\z)').Value
    P02-Expect $authorization 'Status' 'Approved'
    P02-Expect $authorization 'AuthorizedPhase' 'P03'
} else {
    P02-Require (-not (Test-Path $future) -or @(Get-ChildItem -LiteralPath $future -Recurse -File).Count -eq 0) 'P03 execution is not authorized.'
}
if ($phaseStatus -in @('InReview', 'Done')) {
    P02-Require ($number -eq 9 -and $execution -eq 'Verified' -and (Test-Path $nextHandoffPath)) 'Phase review requires all nine verified steps and Draft P03 handoff.'
    foreach ($name in @('LockedRestore', 'ReleaseBuild', 'Tests', 'Format', 'Roadmap', 'GitRules', 'DiffCheck', 'CI')) { P02-Require ($checks.ContainsKey($name) -and $checks[$name].status -eq 'Passed') "Missing phase verification: $name" }
    P02-Require ($evidence.tests.passed -ge 133 -and $evidence.tests.failed -eq 0 -and $evidence.tests.skipped -eq 0) 'No failed/skipped tests may be hidden.'
    P02-Require ($null -ne $evidence.ci -and $evidence.ci.event -eq 'pull_request' -and $evidence.ci.conclusion -eq 'success' -and $evidence.ci.headSha -match '^[a-f0-9]{40}$' -and $evidence.ci.runId -gt 0) 'Actual successful PR CI is required.'
    P02-Require (@($evidence.ci.jobs).Count -ge 3 -and @($evidence.ci.jobs | Where-Object conclusion -ne 'success').Count -eq 0) 'Every required CI job must succeed.'
}
if ($closed) {
    P02-Require ($number -eq 9) 'Closure requires all nine steps.'
    foreach ($name in @('ClosureLockedRestore', 'ClosureReleaseBuild', 'ClosureTests', 'ClosureFormat')) { P02-Require ($checks.ContainsKey($name) -and $checks[$name].status -eq 'Passed') "Missing new closure evidence: $name" }
    $closure = Read-P02 'planning/phase-02/CLOSURE.md'
    P02-ClosureFields $closure
}
Write-Output "PASS: authorized P02 $current $phaseStatus; sequential technical handoffs; human approval $approval; P03 execution awaits merge and post-merge gate."
