param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Read-Record([string]$Path) { Get-Content -LiteralPath (Join-Path $Root $Path) -Raw }
function Field([string]$Text, [string]$Name, [string]$Prefix = '') {
    $values = [regex]::Matches($Text, '(?m)^' + [regex]::Escape($Prefix + $Name) + ': ([^\r\n]+)\r?$')
    if ($values.Count -ne 1) { throw "Expected exactly one field: $Name" }
    return $values[0].Groups[1].Value
}
function Require([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Expect([string]$Text, [string]$Name, [string]$Value, [string]$Prefix = '') {
    Require ((Field $Text $Name $Prefix) -eq $Value) "Invalid phase gate field: $Name (expected $Value)"
}
$state = Read-Record 'docs/CURRENT_STATE.md'
$decisions = Read-Record 'docs/DECISIONS.md'
$decision = [regex]::Match($decisions, '(?ms)^## D012 .*?(?=^## |\z)').Value
Expect $decision 'Status' 'Approved'
Expect $decision 'EffectiveFrom' 'P01-S07'
Expect $decision 'ExecutionMode' 'Phase'
Expect $state 'Phase' 'P01' '- '
Expect $state 'ExecutionMode' 'Phase' '- '
$current = Field $state 'Step' '- '
$status = Field $state 'Status' '- '
Require ($current -in @('P01-S07', 'P01-S08')) 'Phase execution must remain in authorized P01 steps.'
Require ($status -in @('InProgress', 'InReview', 'Done')) 'Unsupported P01 state.'
$closed = $status -eq 'Done'
$approval = if ($closed) { 'Approved' } else { 'Pending' }
$manual = if ($closed) { 'Deferred' } else { 'Pending' }
Expect $state 'UserApproval' $approval '- '
Expect $state 'V4' $approval '- '
function Closure-Fields([string]$Text, [string]$Prefix = '') {
    foreach ($pair in @(@('ApprovalDate', '2026-11-10'), @('ReviewedHead', '4b4d5a574dd459eda0e091311eab908439cb2fd0'), @('AcceptanceDecision', 'D013'), @('AcceptanceBasis', 'CodeTestsCI'))) { Expect $Text $pair[0] $pair[1] $Prefix }
}
if ($closed) {
    Require ($current -eq 'P01-S08') 'Only completed S08 can close P01.'
    Expect $state 'ReviewStatus' 'Approved' '- '
    Expect $state 'P02Status' 'NotStarted' '- '
    Closure-Fields $state '- '
    $acceptance = [regex]::Match($decisions, '(?ms)^## D013 .*?(?=^## |\z)').Value
    foreach ($pair in @(@('Status', 'Approved'), @('ApprovalDate', '2026-11-10'), @('ReviewedHead', '4b4d5a574dd459eda0e091311eab908439cb2fd0'), @('AcceptanceBasis', 'CodeTestsCI'), @('ClosureAuthorization', 'Approved'), @('MergeAuthorization', 'Approved'), @('P02Execution', 'NotStarted'))) { Expect $acceptance $pair[0] $pair[1] }
    foreach ($field in @('UserScalarReview', 'UserPostmanReview', 'EducationalWalkthrough')) {
        Expect $acceptance $field 'Deferred'
        Expect $state $field 'Deferred' '- '
    }
}
Expect $state 'ActiveStepFile' ("planning/phase-01/steps/" + $current.Substring(4) + '.md') '- '
$evidence = Read-Record 'planning/phase-01/verification.json' | ConvertFrom-Json
Require ($evidence.phase -eq 'P01') 'Evidence phase mismatch.'
$checks = @{}
foreach ($check in $evidence.checks) {
    Require (-not $checks.ContainsKey($check.name)) 'Duplicate verification name.'
    Require ($check.status -in @('Passed', 'Failed', 'Pending', 'Unavailable')) 'Unknown verification status.'
    if ($check.status -eq 'Passed') {
        Require ($null -ne $check.exitCode -and $check.exitCode -eq 0) "Cannot claim Passed for a failed/unrun check: $($check.name)"
        Require (-not [string]::IsNullOrWhiteSpace($check.command)) 'Passed check needs its actual command/procedure.'
    } elseif ($check.status -eq 'Failed') {
        Require ($null -ne $check.exitCode -and $check.exitCode -ne 0) 'Failed check needs its nonzero exit code.'
    }
    $checks[$check.name] = $check
}
function Passed([string[]]$Names) {
    foreach ($name in $Names) {
        Require ($checks.ContainsKey($name) -and $checks[$name].status -eq 'Passed') "Required verification is not Passed: $name"
    }
}
Passed @('S07Build', 'S07Tests', 'ScalarBrowser', 'PostmanTool')
$s07 = Read-Record 'planning/phase-01/steps/S07.md'
$s08Path = Join-Path $Root 'planning/phase-01/steps/S08.md'
$s08 = if (Test-Path -LiteralPath $s08Path) { Read-Record 'planning/phase-01/steps/S08.md' } else { '' }
foreach ($entry in @(@{Text = $s07; Id = 'S07'; Previous = 'P01-S06'; Next = 'P01-S08'}, @{Text = $s08; Id = 'S08'; Previous = 'P01-S07'; Next = 'PhaseReview'})) {
    if (-not $entry.Text) { continue }
    $text = $entry.Text
    foreach ($pair in @(@('Phase', 'P01'), @('PreviousStep', $entry.Previous), @('NextStep', $entry.Next), @('ExecutionMode', 'Phase'), @('UserApproval', $approval), @('ReviewStatus', $approval), @('V4', $approval))) {
        Expect $text $pair[0] $pair[1]
    }
    if ($closed) {
        Expect $text 'Status' 'Done'
        Expect $text 'ExecutionStatus' 'Verified'
        Expect $text 'VerificationStatus' 'Passed'
        Closure-Fields $text
    } else {
        Require ((Field $text 'Status') -in @('InProgress', 'InReview')) 'Unapproved step cannot claim Done.'
    }
    $execution = Field $text 'ExecutionStatus'
    $verification = Field $text 'VerificationStatus'
    Require ($execution -in @('InProgress', 'Verified')) 'Invalid internal execution state.'
    Require ($verification -in @('Pending', 'Passed', 'Failed', 'Unavailable')) 'Invalid step verification state.'
    Require (($execution -eq 'Verified') -eq ($verification -eq 'Passed')) 'Verified execution requires Passed verification and vice versa.'
    $template = Read-Record 'planning/templates/STEP_TEMPLATE.md'
    foreach ($heading in [regex]::Matches($template, '(?m)^## .+$')) {
        Require ($text.Contains($heading.Value.Trim())) "Missing step heading: $($entry.Id)"
    }
}
Expect $s07 'Status' $(if ($closed) { 'Done' } else { 'InReview' })
Expect $s07 'ExecutionStatus' 'Verified'
Expect $s07 'VerificationStatus' 'Passed'
foreach ($field in @('UserScalarReview', 'UserPostmanReview')) { Expect $s07 $field $manual }
foreach ($field in @('ScalarBrowserVerification', 'PostmanVerification')) { Expect $s07 $field 'Passed' }
Expect $s07 'LoggingPolicy' 'PipelineOnly; HostLogsBlocked; OperationalDiagnosticsIncomplete'
$internal = Read-Record 'planning/phase-01/handoffs/S07-to-S08.md'
foreach ($pair in @(@('From', 'P01-S07'), @('To', 'P01-S08'), @('Status', $(if ($closed) { 'Ready' } else { 'InternalReady' })), @('ExecutionMode', 'Phase'), @('UserApproval', $approval), @('ReviewStatus', $approval))) { Expect $internal $pair[0] $pair[1] }
if ($current -eq 'P01-S08') { Require ([bool]$s08) 'S08 must have its execution record.' }
if ($s08 -and (Field $s08 'ExecutionStatus') -eq 'Verified') {
    Passed @('Restore', 'Build', 'Tests', 'Format', 'Roadmap', 'GitRules', 'PhaseGates', 'Diff', 'DockerApi', 'DockerSql', 'DockerSmoke', 'CI')
    $ci = $evidence.ci
    Require ($ci.event -eq 'pull_request' -and $ci.conclusion -eq 'success' -and $ci.runId -gt 0 -and $ci.headSha -match '^[a-f0-9]{40}$') 'S08 requires a real successful PR workflow.'
    Require ($ci.url -eq "https://github.com/mohamedfathy5242420-cloud/HawalekNow/actions/runs/$($ci.runId)") 'CI run URL mismatch.'
    Require (@($ci.jobs).Count -eq 3) 'All three CI jobs must be recorded.'
    foreach ($name in @('Documentation and Git rules', 'Restore, build, tests and format', 'API and SQL image builds')) {
        $job = @($ci.jobs | Where-Object name -eq $name)
        Require ($job.Count -eq 1 -and $job[0].conclusion -eq 'success') "CI job did not succeed: $name"
    }
    Require ($evidence.tests.failed -eq 0 -and $evidence.tests.skipped -eq 0 -and $evidence.tests.passed -eq ($evidence.tests.architecture + $evidence.tests.integration + $evidence.tests.unit)) 'Test evidence totals/failures disagree.'
}
$review = Read-Record 'planning/phase-01/PHASE_REVIEW.md'
Expect $review 'Phase' 'P01'
Expect $review 'Status' $status
foreach ($field in @('UserApproval', 'ReviewStatus', 'V4')) { Expect $review $field $approval }
foreach ($field in @('UserScalarReview', 'UserPostmanReview', 'EducationalWalkthrough')) { Expect $review $field $manual }
if ($closed) { Closure-Fields $review; Closure-Fields $internal }
if ($status -eq 'InReview') {
    Require ([bool]$s08) 'Phase review requires S08 execution.'
    Expect $s08 'ExecutionStatus' 'Verified'
    Expect $s08 'Status' 'InReview'
}
$handoff = Read-Record 'planning/phase-01/handoffs/S08-to-P02.md'
foreach ($pair in @(@('From', 'P01-S08'), @('To', 'P02-S01'), @('Status', $(if ($closed) { 'Ready' } else { 'Draft' })), @('UserApproval', $approval), @('ReviewStatus', $approval))) { Expect $handoff $pair[0] $pair[1] }
if ($closed) {
    Closure-Fields $handoff
    Expect $handoff 'P02Status' 'NotStarted'
    foreach ($field in @('UserScalarReview', 'UserPostmanReview', 'EducationalWalkthrough')) { Expect $handoff $field 'Deferred' }
    $closure = Read-Record 'planning/phase-01/CLOSURE.md'
    Expect $closure 'AcceptanceDecision' 'D013'
    Expect $closure 'ApprovalDate' '2026-11-10'
    Expect $closure 'ReviewedHead' '4b4d5a574dd459eda0e091311eab908439cb2fd0'
}
foreach ($text in @($internal, $handoff)) {
    foreach ($heading in [regex]::Matches((Read-Record 'planning/templates/HANDOFF_TEMPLATE.md'), '(?m)^## .+$')) { Require ($text.Contains($heading.Value.Trim())) 'Missing handoff heading.' }
}
$future = Join-Path $Root 'planning/phase-02'
Require (-not (Test-Path $future) -or @(Get-ChildItem -LiteralPath $future -Recurse -File).Count -eq 0) 'P02 execution cannot start before phase approval and authorization.'
$map = Read-Record 'planning/ROADMAP.md'
foreach ($id in @('P01-S07', 'P01-S08')) {
    $block = [regex]::Match($map, '(?ms)^### ' + $id + ' .*?(?=^### |\z)').Value
    $step = if ($id -eq 'P01-S07') { $s07 } else { $s08 }
    $mapStatus = [regex]::Matches($block, '(?m)^- Status: ([^؛\r\n]+)(?:؛|\r?$)')
    Require ($mapStatus.Count -eq 1 -and $mapStatus[0].Groups[1].Value -eq (Field $step 'Status')) "Roadmap state differs: $id"
    foreach ($name in @('ExecutionStatus', 'VerificationStatus')) {
        $values = [regex]::Matches($block, '(?m)(?:^|؛ )' + $name + ': ([^؛\r\n]+)(?:؛|\r?$)')
        Require ($values.Count -eq 1 -and $values[0].Groups[1].Value -eq (Field $step $name)) "Roadmap $name differs: $id"
    }
}
if ($closed) {
    $next = [regex]::Match($map, '(?ms)^### P02-S01 .*?(?=^### |\z)').Value
    Require ($next -match '(?m)^- Status: NotStarted؛') 'P02 roadmap must remain NotStarted.'
    foreach ($id in @('P01-S07', 'P01-S08')) {
        $block = [regex]::Match($map, '(?ms)^### ' + $id + ' .*?(?=^### |\z)').Value
        foreach ($value in @('UserApproval: Approved', 'ReviewStatus: Approved', 'V4: Approved', 'ApprovalDate: 2026-11-10', 'ReviewedHead: 4b4d5a574dd459eda0e091311eab908439cb2fd0')) { Require ($block.Contains($value)) "Missing closed roadmap field: $id $value" }
    }
}
Write-Output "PASS: D012/D013 phase gate; state=$status; approval=$approval; personal educational reviews=$manual; all technical/CI evidence required; P02 remains unstarted."
Write-Output 'LIMIT: recorded evidence consistency only; this script does not execute .NET/Docker or query GitHub. CI and runtime evidence must be collected separately.'
