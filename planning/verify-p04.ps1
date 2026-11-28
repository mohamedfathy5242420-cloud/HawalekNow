param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Read-P04($path) { Get-Content -LiteralPath (Join-Path $Root $path) -Raw }
function P04-Require([bool]$condition, $message) { if (-not $condition) { throw $message } }
function P04-Field($text, $name, $prefix = '') {
    $matches = [regex]::Matches($text, '(?m)^' + [regex]::Escape($prefix + $name) + ': ([^\r\n]+)\r?$')
    P04-Require ($matches.Count -eq 1) "P04 requires exactly one field $name"
    $matches[0].Groups[1].Value
}
function P04-Expect($text, $name, $expected, $prefix = '') { P04-Require ((P04-Field $text $name $prefix) -eq $expected) "P04 invalid $name" }
$state = Read-P04 'docs/CURRENT_STATE.md'
foreach ($pair in @(@('Phase', 'P04'), @('ExecutionMode', 'Phase'), @('ExecutionAuthorization', 'D021'), @('UserApproval', 'Pending'), @('ReviewStatus', 'Pending'), @('V4', 'Pending'), @('P05Status', 'NotStarted'), @('BaseMain', '63b189618a9c856151920da7669a4e2c9af86461'))) { P04-Expect $state $pair[0] $pair[1] '- ' }
$authorization = [regex]::Match((Read-P04 'docs/DECISIONS.md'), '(?ms)^## D021 .*?(?=^## |\z)').Value
foreach ($pair in @(@('Status', 'Approved'), @('AuthorizedPhase', 'P04'), @('ExecutionMode', 'Phase'), @('PhaseUserApproval', 'Pending'), @('MergeAuthorization', 'NotAuthorized'), @('P05Execution', 'NotAuthorized'), @('BaseMain', '63b189618a9c856151920da7669a4e2c9af86461'), @('PredecessorAcceptance', 'D020'))) { P04-Expect $authorization $pair[0] $pair[1] }
$status = P04-Field $state 'Status' '- '
P04-Require ($status -in @('InProgress', 'InReview')) 'P04 cannot be approved or closed under execution authorization.'
$stepId = P04-Field $state 'Step' '- '
P04-Require ($stepId -match '^P04-S0[1-7]$') 'Only P04 S01 through S07 are authorized.'
$number = [int]$stepId.Substring(5)
P04-Expect $state 'ActiveStepFile' ("planning/phase-04/steps/S{0:d2}.md" -f $number) '- '
$evidence = Read-P04 'planning/phase-04/verification.json' | ConvertFrom-Json
P04-Require ($evidence.phase -eq 'P04' -and $evidence.authorization -eq 'D021' -and $evidence.baseMain -eq '63b189618a9c856151920da7669a4e2c9af86461') 'P04 evidence identity mismatch.'
P04-Require ($evidence.predecessor.pr -eq 10 -and $evidence.predecessor.merged -eq $true -and $evidence.predecessor.acceptance -eq 'D020' -and $evidence.predecessor.mergeSha -eq $evidence.baseMain -and $evidence.predecessor.postMergeVerification -eq 'Passed' -and $evidence.predecessor.ciRun -eq 45600216946) 'Verified P03 predecessor required.'
P04-Require ($evidence.userApproval -eq 'Pending' -and $evidence.v4 -eq 'Pending' -and $evidence.p05Handoff -eq 'Draft') 'Do not fabricate phase approval or start P05.'
$checks = @{}
foreach ($check in $evidence.checks) {
    P04-Require (-not $checks.ContainsKey($check.name)) 'Duplicate P04 check.'
    P04-Require ($check.status -in @('Passed', 'Failed', 'Pending', 'Unavailable')) 'Invalid P04 check status.'
    if ($check.status -eq 'Passed') { P04-Require ($check.exitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($check.command)) 'Passed requires a real command and exit0.' }
    if ($check.status -eq 'Failed') { P04-Require ($null -ne $check.exitCode -and $check.exitCode -ne 0) 'Failed requires its nonzero exit.' }
    $checks[$check.name] = $check
}
$map = Read-P04 'planning/ROADMAP.md'
$template = Read-P04 'planning/templates/STEP_TEMPLATE.md'
foreach ($index in 1..7) {
    $id = 'S{0:d2}' -f $index
    $path = "planning/phase-04/steps/$id.md"
    if ($index -gt $number) { P04-Require (-not (Test-Path (Join-Path $Root $path))) 'A future P04 step started before its predecessor.'; continue }
    $step = Read-P04 $path
    foreach ($pair in @(@('Phase', 'P04'), @('ExecutionMode', 'Phase'), @('UserApproval', 'Pending'), @('ReviewStatus', 'Pending'), @('V4', 'Pending'))) { P04-Expect $step $pair[0] $pair[1] }
    P04-Expect $step 'PreviousStep' $(if ($index -eq 1) { 'P03-S07' } else { 'P04-S{0:d2}' -f ($index - 1) })
    P04-Expect $step 'NextStep' $(if ($index -eq 7) { 'PhaseReview' } else { 'P04-S{0:d2}' -f ($index + 1) })
    $stepStatus = P04-Field $step 'Status'
    $execution = P04-Field $step 'ExecutionStatus'
    $verification = P04-Field $step 'VerificationStatus'
    P04-Require ($stepStatus -in @('InProgress', 'InReview') -and $execution -in @('InProgress', 'Verified') -and $verification -in @('Pending', 'Failed', 'Passed', 'Unavailable')) 'Invalid P04 step state.'
    P04-Require (($execution -eq 'Verified') -eq ($verification -eq 'Passed')) 'Verified requires Passed.'
    foreach ($heading in [regex]::Matches($template, '(?m)^## .+$')) { P04-Require ($step.Contains($heading.Value.Trim())) "Missing step heading $id" }
    $block = [regex]::Match($map, '(?ms)^### P04-' + $id + ' .*?(?=^### |\z)').Value
    P04-Require ($block -match ('(?m)^- Status: ' + $stepStatus + '؛')) "Roadmap differs for $id"
    if ($index -lt $number -or $execution -eq 'Verified') {
        P04-Expect $step 'Status' 'InReview'
        P04-Expect $step 'ExecutionStatus' 'Verified'
        foreach ($kind in @('Build', 'Tests', 'Sql')) { P04-Require ($checks.ContainsKey("$id$kind") -and $checks["$id$kind"].status -eq 'Passed') "Missing $id$kind evidence" }
        if ($index -lt 7) {
            $handoff = Read-P04 ("planning/phase-04/handoffs/$id-to-S{0:d2}.md" -f ($index + 1))
            foreach ($pair in @(@('Status', 'InternalReady'), @('UserApproval', 'Pending'), @('ReviewStatus', 'Pending'))) { P04-Expect $handoff $pair[0] $pair[1] }
        }
    }
}
$review = Read-P04 'planning/phase-04/PHASE_REVIEW.md'
foreach ($field in @('UserApproval', 'ReviewStatus', 'V4')) { P04-Expect $review $field 'Pending' }
P04-Expect $review 'Status' $status
if ($status -eq 'InReview') {
    P04-Require ($number -eq 7 -and $execution -eq 'Verified') 'Final review requires all seven verified steps.'
    foreach ($name in @('LockedRestore', 'ReleaseBuild', 'Tests', 'Format', 'Roadmap', 'GitRules', 'DiffCheck', 'Docker', 'CI')) { P04-Require ($checks.ContainsKey($name) -and $checks[$name].status -eq 'Passed') "Missing final $name check" }
    P04-Require ($evidence.tests.failed -eq 0 -and $evidence.tests.skipped -eq 0 -and $evidence.tests.passed -gt 0 -and $evidence.tests.passed -eq ($evidence.tests.architecture + $evidence.tests.integration + $evidence.tests.unit)) 'Consistent full solution results required.'
    P04-Require ($evidence.ci.event -eq 'pull_request' -and $evidence.ci.headSha -match '^[a-f0-9]{40}$' -and $evidence.ci.status -eq 'completed' -and $evidence.ci.conclusion -eq 'success' -and @($evidence.ci.jobs).Count -eq 3 -and @($evidence.ci.jobs | Where-Object conclusion -ne 'success').Count -eq 0) 'Actual successful PR CI required.'
    P04-Expect (Read-P04 'planning/phase-04/handoffs/S07-to-P05.md') 'Status' 'Draft'
}
$future = Join-Path $Root 'planning/phase-05'
P04-Require (-not (Test-Path $future) -or @(Get-ChildItem $future -Recurse -File).Count -eq 0) 'P05 is not authorized.'
Write-Output "PASS: D021 with verified P03 merge; P04 $stepId $status; approval/V4 Pending; P05 prohibited."
