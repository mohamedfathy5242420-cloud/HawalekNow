param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Read-P03($path) { Get-Content -LiteralPath (Join-Path $Root $path) -Raw }
function P03-Field($text, $name, $prefix = '') {
    $found = [regex]::Matches($text, '(?m)^' + [regex]::Escape($prefix + $name) + ': ([^\r\n]+)\r?$')
    if ($found.Count -ne 1) { throw "P03 expected exactly one field: $name" }
    $found[0].Groups[1].Value
}
function P03-Require([bool]$condition, $message) { if (-not $condition) { throw $message } }
function P03-Expect($text, $name, $value, $prefix = '') { P03-Require ((P03-Field $text $name $prefix) -eq $value) "P03 invalid field: $name" }
$state = Read-P03 'docs/CURRENT_STATE.md'
foreach ($pair in @(@('Phase', 'P03'), @('ExecutionAuthorization', 'D018'), @('ExecutionMode', 'Phase'), @('UserApproval', 'Pending'), @('ReviewStatus', 'Pending'), @('V4', 'Pending'), @('P04Status', 'NotStarted'))) { P03-Expect $state $pair[0] $pair[1] '- ' }
$current = P03-Field $state 'Step' '- '
P03-Require ($current -match '^P03-S0[1-7]$') 'Only S01 through S07 are authorized.'
$number = [int]$current.Substring(5)
$status = P03-Field $state 'Status' '- '
P03-Require ($status -in @('InProgress', 'InReview')) 'P03 may not be closed or approved.'
P03-Expect $state 'ActiveStepFile' ("planning/phase-03/steps/S{0:d2}.md" -f $number) '- '
$decisions = Read-P03 'docs/DECISIONS.md'
$authorization = [regex]::Match($decisions, '(?ms)^## D018 .*?(?=^## |\z)').Value
foreach ($pair in @(@('Status', 'Approved'), @('AuthorizedPhase', 'P03'), @('PhaseUserApproval', 'Pending'), @('MergeAuthorization', 'NotAuthorized'), @('P04Execution', 'NotAuthorized'), @('BaseMain', '5e4250ff31f84c8806649c7ef1786574da7dd00a'))) { P03-Expect $authorization $pair[0] $pair[1] }
$source = [regex]::Match($decisions, '(?ms)^## D019 .*?(?=^## |\z)').Value
foreach ($pair in @(@('Status', 'Approved'), @('SourceCommit', '76b47b22872288c0d8b0c757975980e6986945e8'), @('Completeness', 'NotClaimed'))) { P03-Expect $source $pair[0] $pair[1] }
$receipt = Read-P03 'planning/phase-02/MERGE_RECEIPT.md'
P03-Expect $receipt 'MergeSha' '5e4250ff31f84c8806649c7ef1786574da7dd00a'
P03-Expect $receipt 'PostMergeVerification' 'Passed'
P03-Expect (Read-P03 'planning/phase-02/handoffs/S09-to-P03.md') 'Status' 'Ready'
$evidence = Read-P03 'planning/phase-03/verification.json' | ConvertFrom-Json
P03-Require ($evidence.phase -eq 'P03' -and $evidence.predecessor.merged -eq $true -and $evidence.predecessor.postMergeVerification -eq 'Passed' -and $evidence.predecessor.mergeSha -eq '5e4250ff31f84c8806649c7ef1786574da7dd00a') 'Verified P02 merge required.'
P03-Require ($evidence.source.commit -eq (P03-Field $source 'SourceCommit') -and $evidence.source.completeEgyptClaimed -eq $false) 'Evidence must preserve the accepted source version and coverage limits.'
$checks = @{}
foreach ($check in $evidence.checks) {
    P03-Require (-not $checks.ContainsKey($check.name)) 'Duplicate check name.'
    P03-Require ($check.status -in @('Passed', 'Pending', 'Failed', 'Unavailable')) 'Invalid check status.'
    if ($check.status -eq 'Passed') { P03-Require ($check.exitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($check.command)) 'Passed requires an actual command and exit0.' }
    if ($check.status -eq 'Failed') { P03-Require ($null -ne $check.exitCode -and $check.exitCode -ne 0) 'Failed requires a nonzero exit.' }
    $checks[$check.name] = $check
}
$map = Read-P03 'planning/ROADMAP.md'
$template = Read-P03 'planning/templates/STEP_TEMPLATE.md'
foreach ($index in 1..7) {
    $id = 'S{0:d2}' -f $index
    $path = "planning/phase-03/steps/$id.md"
    if ($index -gt $number) { P03-Require (-not (Test-Path (Join-Path $Root $path))) 'Future step started before its predecessor.'; continue }
    $step = Read-P03 $path
    foreach ($pair in @(@('Phase', 'P03'), @('ExecutionMode', 'Phase'), @('UserApproval', 'Pending'), @('ReviewStatus', 'Pending'), @('V4', 'Pending'))) { P03-Expect $step $pair[0] $pair[1] }
    P03-Expect $step 'PreviousStep' $(if ($index -eq 1) { 'P02-S09' } else { 'P03-S{0:d2}' -f ($index - 1) })
    P03-Expect $step 'NextStep' $(if ($index -eq 7) { 'PhaseReview' } else { 'P03-S{0:d2}' -f ($index + 1) })
    $stepStatus = P03-Field $step 'Status'
    $execution = P03-Field $step 'ExecutionStatus'
    $verification = P03-Field $step 'VerificationStatus'
    P03-Require ($stepStatus -in @('InProgress', 'InReview') -and $execution -in @('InProgress', 'Verified')) 'Invalid step state.'
    P03-Require ($verification -in @('Pending', 'Failed', 'Passed', 'Unavailable')) 'Invalid verification state.'
    P03-Require (($execution -eq 'Verified') -eq ($verification -eq 'Passed')) 'Verified requires Passed.'
    foreach ($heading in [regex]::Matches($template, '(?m)^## .+$')) { P03-Require ($step.Contains($heading.Value.Trim())) "Missing step heading $id" }
    $block = [regex]::Match($map, '(?ms)^### P03-' + $id + ' .*?(?=^### |\z)').Value
    P03-Require ($block -match ('(?m)^- Status: ' + $stepStatus + '؛')) "Roadmap disagrees with $id"
    if ($index -lt $number -or $execution -eq 'Verified') {
        P03-Expect $step 'Status' 'InReview'
        P03-Expect $step 'ExecutionStatus' 'Verified'
        foreach ($kind in @('Build', 'Tests', 'Sql')) { P03-Require ($checks.ContainsKey("$id$kind") -and $checks["$id$kind"].status -eq 'Passed') "Missing technical evidence $id$kind" }
        if ($index -lt $number) { P03-Expect (Read-P03 ("planning/phase-03/handoffs/$id-to-S{0:d2}.md" -f ($index + 1))) 'Status' 'InternalReady' }
    }
}
if ($status -eq 'InReview') {
    P03-Require ($number -eq 7 -and $execution -eq 'Verified') 'All seven verified steps are required.'
    foreach ($name in @('LockedRestore', 'ReleaseBuild', 'Tests', 'Format', 'Roadmap', 'GitRules', 'DiffCheck', 'CI')) { P03-Require ($checks.ContainsKey($name) -and $checks[$name].status -eq 'Passed') "Missing final check $name" }
    P03-Require ($evidence.tests.failed -eq 0 -and $evidence.tests.skipped -eq 0 -and $evidence.tests.passed -gt 0 -and $evidence.tests.passed -eq ($evidence.tests.architecture + $evidence.tests.integration + $evidence.tests.unit)) 'Actual full solution results and consistent totals required.'
    P03-Require ($evidence.ci.event -eq 'pull_request' -and $evidence.ci.conclusion -eq 'success') 'Actual PR workflow success required.'
    P03-Expect (Read-P03 'planning/phase-03/handoffs/S07-to-P04.md') 'Status' 'Draft'
    P03-Expect (Read-P03 'planning/phase-03/PHASE_REVIEW.md') 'UserApproval' 'Pending'
}
$future = Join-Path $Root 'planning/phase-04'
P03-Require (-not (Test-Path $future) -or @(Get-ChildItem $future -Recurse -File).Count -eq 0) 'P04 is not authorized.'
Write-Output "PASS: D018/D019 and verified P02 merge; P03 $current $status; approval/V4 Pending; P04 prohibited."
