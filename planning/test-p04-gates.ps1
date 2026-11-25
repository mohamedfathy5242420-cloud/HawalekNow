param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$fixture = Join-Path $Root ('tmp/p04-gates-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
$paths = @(& git -c "safe.directory=$([IO.Path]::GetFullPath($Root).Replace('\','/'))" -C $Root ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate P04 fixture.' }
foreach ($path in $paths | Sort-Object -Unique) {
    if ($path -eq 'docs/TARGET_SCHEDULE.md') { continue }
    $destination = Join-Path $fixture $path
    [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
    [IO.File]::Copy((Join-Path $Root $path), $destination)
}
function Check-P04 { & (Join-Path $fixture 'planning/verify-roadmap.ps1') -Root $fixture | Out-Null }
$script:p04Rejections = 0
function Reject-P04($label, $path, [scriptblock]$mutation) {
    $destination = Join-Path $fixture $path
    $original = [IO.File]::ReadAllText($destination)
    try {
        $changed = & $mutation $original
        if ($changed -eq $original) { throw "No mutation for $label" }
        [IO.File]::WriteAllText($destination, $changed)
        $rejected = $false
        try { Check-P04 } catch { $rejected = $true }
        if (-not $rejected) { throw "Invalid P04 state accepted: $label" }
        $script:p04Rejections++
    } finally { [IO.File]::WriteAllText($destination, $original) }
}
Check-P04
Reject-P04 'authorization missing' 'docs/DECISIONS.md' { param($t) $t.Replace('## D021 ', '## D099 ') }
Reject-P04 'P04 merge falsely authorized' 'docs/DECISIONS.md' { param($t) $t.Replace('MergeAuthorization: NotAuthorized', 'MergeAuthorization: Approved') }
Reject-P04 'P03 acceptance missing' 'docs/DECISIONS.md' { param($t) $t.Replace('## D020 ', '## D099 ') }
Reject-P04 'P03 approval missing' 'docs/P03_CLOSED_STATE.md' { param($t) $t.Replace('- UserApproval: Approved', '- UserApproval: Pending') }
Reject-P04 'P03 merge verification missing' 'planning/phase-03/MERGE_RECEIPT.md' { param($t) $t.Replace('PostMergeVerification: Passed', 'PostMergeVerification: Pending') }
Reject-P04 'P03 handoff not Ready' 'planning/phase-03/handoffs/S07-to-P04.md' { param($t) $t.Replace('Status: Ready', 'Status: Draft') }
Reject-P04 'P03 main CI wrong event' 'planning/phase-03/merge-verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.ci.event='pull_request'; $e|ConvertTo-Json -Depth 35 }
Reject-P04 'P03 main CI failed' 'planning/phase-03/merge-verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.ci.conclusion='failure'; $e|ConvertTo-Json -Depth 35 }
Reject-P04 'P04 different base' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- BaseMain: 63b189618a9c856151920da7669a4e2c9af86461', '- BaseMain: 0000000000000000000000000000000000000000') }
Reject-P04 'P04 falsely Done' 'docs/CURRENT_STATE.md' { param($t) $t -replace '(?m)^- Status: [^\r\n]+', '- Status: Done' }
Reject-P04 'P04 approval fabricated' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- UserApproval: Pending', '- UserApproval: Approved') }
Reject-P04 'P04 manual V4 fabricated' 'planning/phase-04/steps/S01.md' { param($t) $t.Replace('V4: Pending', 'V4: Passed') }
Reject-P04 'duplicate step field' 'planning/phase-04/steps/S01.md' { param($t) $t+"`nStatus: InReview`n" }
Reject-P04 'wrong active step' 'docs/CURRENT_STATE.md' { param($t) $t -replace '(?m)^- ActiveStepFile: [^\r\n]+', '- ActiveStepFile: planning/phase-04/steps/S00.md' }
Reject-P04 'P05 started' 'docs/CURRENT_STATE.md' { param($t) $t.Replace('- P05Status: NotStarted', '- P05Status: InProgress') }
Reject-P04 'predecessor unmerged' 'planning/phase-04/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.predecessor.merged=$false; $e|ConvertTo-Json -Depth 35 }
Reject-P04 'evidence approval fabricated' 'planning/phase-04/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.userApproval='Approved'; $e|ConvertTo-Json -Depth 35 }
$state = Get-Content (Join-Path $fixture 'docs/CURRENT_STATE.md') -Raw
$current = [int][regex]::Match($state, '(?m)^- Step: P04-S0([1-7])').Groups[1].Value
if ((Get-Content (Join-Path $fixture 'planning/phase-04/steps/S01.md') -Raw).Contains('ExecutionStatus: Verified')) {
    Reject-P04 'verified S01 without tests' 'planning/phase-04/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.checks=@($e.checks|Where-Object name -ne 'S01Tests'); $e|ConvertTo-Json -Depth 35 }
    Reject-P04 'verified S01 without SQL' 'planning/phase-04/verification.json' { param($t) $e=$t|ConvertFrom-Json; $e.checks=@($e.checks|Where-Object name -ne 'S01Sql'); $e|ConvertTo-Json -Depth 35 }
}
if ($current -lt 7) {
    $futurePath = Join-Path $fixture ('planning/phase-04/steps/S{0:d2}.md' -f ($current + 1))
    try {
        [IO.File]::WriteAllText($futurePath, '# Unauthorized next step')
        $rejected = $false
        try { Check-P04 } catch { $rejected = $true }
        if (-not $rejected) { throw 'A future step was accepted before its predecessor.' }
        $script:p04Rejections++
    } finally { Remove-Item -LiteralPath $futurePath }
}
Check-P04
Write-Output "PASS: actual P04 state accepted; $script:p04Rejections unauthorized predecessor/phase/approval/evidence states rejected."
