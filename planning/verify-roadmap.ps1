param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $Root 'Hawalek_Now_Backend_Master_Execution_Plan_AR.docx'))
try {
  $reader = [IO.StreamReader]::new($zip.GetEntry('word/document.xml').Open())
  [xml]$xml = $reader.ReadToEnd()
  $reader.Dispose()
  $ns = [Xml.XmlNamespaceManager]::new($xml.NameTable)
  $ns.AddNamespace('w','http://schemas.openxmlformats.org/wordprocessingml/2006/main')
  $source = @(foreach ($table in $xml.SelectNodes('//w:tbl',$ns)) {
    foreach ($row in $table.SelectNodes('w:tr',$ns)) {
      $cells = @($row.SelectNodes('w:tc',$ns) | ForEach-Object { ($_.SelectNodes('.//w:t',$ns) | ForEach-Object {$_.InnerText}) -join '' })
      if ($cells[0] -match '^P\d{2}-S\d{2}$') { [pscustomobject]@{Id=$cells[0];Name=$cells[1];Output=$cells[2];Gate=$cells[3]} }
    }
  })
} finally { $zip.Dispose() }
$mapPath = Join-Path $Root 'planning/ROADMAP.md'
$map = Get-Content -LiteralPath $mapPath -Raw
$ids = @([regex]::Matches($map,'(?m)^### (P\d{2}-S\d{2}) ') | ForEach-Object {$_.Groups[1].Value})
if ($source.Count -ne 76 -or $ids.Count -ne $source.Count -or ($ids | Select-Object -Unique).Count -ne $ids.Count) { throw 'Step count or duplicate mismatch' }
$edges = @{}
for ($i=0; $i -lt $source.Count; $i++) {
  $s=$source[$i]
  if ($ids[$i] -ne $s.Id) { throw "Order mismatch: $($s.Id)" }
  $block=[regex]::Match($map,'(?ms)^### '+[regex]::Escape($s.Id)+' .*?(?=^### |\z)').Value
  foreach ($value in @($s.Name,$s.Output,$s.Gate)) { if (-not $block.Contains($value)) { throw "Source mismatch: $($s.Id)" } }
  $depLine=[regex]::Match($block,'(?m)^- الاعتمادات السابقة: (.+)$').Groups[1].Value
  $deps=@([regex]::Matches($depLine,'\[(P\d{2}-S\d{2})\]') | ForEach-Object {$_.Groups[1].Value})
  $edges[$s.Id]=$deps
  foreach($d in $deps) { if($d -notin $ids) { throw "Missing dependency: $d" }; if([array]::IndexOf($ids,$d) -ge $i){throw "Forward dependency: $($s.Id) -> $d"} }
  if($i -eq 0 -and $deps.Count -ne 0){throw 'First step dependency mismatch'}
  if($i -gt 0 -and ($deps.Count -ne 1 -or $deps[0] -ne $ids[$i-1])){throw "Sequential dependency mismatch: $($s.Id)"}
  if($s.Id.EndsWith('S01') -and $i -gt 0 -and -not $depLine.Contains('Phase Review')){throw 'Missing phase gate'}
}
$visiting=@{}; $visited=@{}
function Visit([string]$Id) {
  if($visiting[$Id]){throw "Cycle at $Id"}
  if($visited[$Id]){return}
  $visiting[$Id]=$true
  foreach($dep in $edges[$Id]){Visit $dep}
  $visiting[$Id]=$false; $visited[$Id]=$true
}
foreach($id in $ids){Visit $id}
$files=@('planning/phase-00/steps/S01.md','planning/phase-00/steps/S02.md','planning/phase-00/handoffs/S01-to-S02.md','planning/ROADMAP.md','planning/phase-00/steps/S03.md','planning/phase-00/handoffs/S02-to-S03.md','planning/phase-00/handoffs/S03-to-S04.md','docs/CURRENT_STATE.md','docs/DECISIONS.md')
if(Test-Path (Join-Path $Root 'planning/phase-00/steps/S04.md')){
  $files += @('planning/phase-00/steps/S04.md','docs/GIT_WORKFLOW.md')
  if(Test-Path (Join-Path $Root 'planning/phase-00/handoffs/S04-to-P01.md')){$files += 'planning/phase-00/handoffs/S04-to-P01.md'}
}
if(Test-Path (Join-Path $Root 'planning/phase-00/PHASE_REVIEW.md')){$files += 'planning/phase-00/PHASE_REVIEW.md'}
foreach($candidate in @('README.md','backend/README.md','docker/README.md','planning/phase-01/steps/S01.md','planning/phase-01/handoffs/S01-to-S02.md')){
  if(Test-Path (Join-Path $Root $candidate)){$files += $candidate}
}
$links=0
foreach($file in $files){
  $path=Join-Path $Root $file
  $text=Get-Content -LiteralPath $path -Raw
  foreach($m in [regex]::Matches($text,'\[[^\]\r\n]+\]\(([^)]+)\)')){
    $target=$m.Groups[1].Value
    if($target -match '^https?://'){continue}
    $parts=$target.Split('#',2)
    $destination=if($parts[0]){[IO.Path]::GetFullPath((Join-Path (Split-Path $path -Parent) $parts[0]))}else{$path}
    if(-not (Test-Path -LiteralPath $destination -PathType Leaf)){throw "Broken link: $file -> $target"}
    if($parts.Count -eq 2){$dest=Get-Content -LiteralPath $destination -Raw;if(-not $dest.Contains('id="'+$parts[1]+'"')){throw "Broken anchor: $target"}}
    $links++
  }
  foreach($m in [regex]::Matches($text,'\bP\d{2}-S\d{2}\b')){if($m.Value -notin $ids){throw "Unknown ID in ${file}: $($m.Value)"}}
}
foreach($kind in @('STEP','HANDOFF')){
  $template=Get-Content (Join-Path $Root "planning/templates/${kind}_TEMPLATE.md") -Raw
  $destination=if($kind -eq 'STEP'){'planning/phase-00/steps/S03.md'}else{'planning/phase-00/handoffs/S02-to-S03.md'}
  $actual=Get-Content (Join-Path $Root $destination) -Raw
  foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $actual.Contains($h.Value.Trim())){throw "Missing template heading: $($h.Value)"}}
}
$s02=Get-Content (Join-Path $Root 'planning/phase-00/steps/S02.md') -Raw
$s03=Get-Content (Join-Path $Root 'planning/phase-00/steps/S03.md') -Raw
$state=Get-Content (Join-Path $Root 'docs/CURRENT_STATE.md') -Raw
if($s02 -notmatch '(?m)^Status: Done\r?$' -or $s03 -notmatch '(?m)^Status: Done\r?$' -or $state -notmatch '(?m)^- Step: (P00-S04|P01-S01|P01-S02)\r?$'){throw 'Closure state mismatch'}
$handoff=Get-Content (Join-Path $Root 'planning/phase-00/handoffs/S03-to-S04.md') -Raw
if($handoff -notmatch '(?m)^Status: Ready\r?$' -or $handoff -notmatch '(?m)^From: P00-S03\r?$' -or $handoff -notmatch '(?m)^To: P00-S04\r?$'){throw 'Closure handoff mismatch'}
$currentStatus=[regex]::Match($state,'(?m)^- Status: (\w+)\r?$').Groups[1].Value
$s04Path=Join-Path $Root 'planning/phase-00/steps/S04.md'
$currentStep=[regex]::Match($state,'(?m)^- Step: (P\d{2}-S\d{2})\r?$').Groups[1].Value
if($currentStep -in @('P01-S01','P01-S02')){
  if($state -notmatch '(?m)^- Phase: P01\r?$'){throw 'P01 phase mismatch'}
  if($currentStep -eq 'P01-S02'){
    if($currentStatus -ne 'NotStarted' -or $state -notmatch '(?m)^- ActiveStepFile: None؛' -or (Test-Path (Join-Path $Root 'planning/phase-01/steps/S02.md'))){throw 'P01-S02 must remain NotStarted in S01 closure'}
    $closedStep=Get-Content (Join-Path $Root 'planning/phase-01/steps/S01.md') -Raw
    $readyHandoff=Get-Content (Join-Path $Root 'planning/phase-01/handoffs/S01-to-S02.md') -Raw
    foreach($field in @('Status: Done','UserApproval: Approved','ApprovalDate: 2026-10-05','Phase: P01','PreviousStep: P00-S04','NextStep: P01-S02')){
      if($closedStep -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "Closed P01-S01 mismatch: $field"}
    }
    foreach($field in @('Status: Ready','From: P01-S01','To: P01-S02','UserApproval: Approved','ApprovalDate: 2026-10-05')){
      if($readyHandoff -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "Ready P01 handoff mismatch: $field"}
    }
    foreach($kind in @('STEP','HANDOFF')){
      $template=Get-Content (Join-Path $Root "planning/templates/${kind}_TEMPLATE.md") -Raw
      $actual=if($kind -eq 'STEP'){$closedStep}else{$readyHandoff}
      foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $actual.Contains($h.Value.Trim())){throw 'P01 closure missing template heading'}}
    }
    $decisionText=Get-Content (Join-Path $Root 'docs/DECISIONS.md') -Raw
    $dockerDecision=[regex]::Match($decisionText,'(?ms)^## D009 .*?(?=^## |\z)').Value
    foreach($token in @('Status: Approved','DeferredTo: P01-S06','ليس Passed')){if(-not $dockerDecision.Contains($token)){throw "Missing approved Docker deferral: $token"}}
    if(-not $closedStep.Contains('Docker مؤجل إلى P01-S06') -or -not $readyHandoff.Contains('مؤجل إلى P01-S06')){throw 'Docker deferral not carried into step and handoff'}
  }else{
  $p01StepPath=Join-Path $Root 'planning/phase-01/steps/S01.md'
  if($currentStatus -eq 'NotStarted'){
    if($state -notmatch '(?m)^- ActiveStepFile: None؛' -or (Test-Path $p01StepPath)){throw 'P01 must remain NotStarted with no execution file'}
  }elseif($currentStatus -in @('InProgress','InReview')){
    if($state -notmatch '(?m)^- ActiveStepFile: planning/phase-01/steps/S01.md\r?$' -or -not (Test-Path $p01StepPath)){throw 'Missing P01-S01 active file'}
    $p01Step=Get-Content $p01StepPath -Raw
    foreach($field in @("Status: $currentStatus",'Phase: P01','PreviousStep: P00-S04','NextStep: P01-S02')){
      if($p01Step -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "P01-S01 state mismatch: $field"}
    }
    $stepTemplate=Get-Content (Join-Path $Root 'planning/templates/STEP_TEMPLATE.md') -Raw
    foreach($h in [regex]::Matches($stepTemplate,'(?m)^## .+$')){if(-not $p01Step.Contains($h.Value.Trim())){throw 'P01-S01 missing template heading'}}
    if($currentStatus -eq 'InReview'){
      $p01Handoff=Get-Content (Join-Path $Root 'planning/phase-01/handoffs/S01-to-S02.md') -Raw
      foreach($field in @('Status: Draft','From: P01-S01','To: P01-S02')){
        if($p01Handoff -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "P01-S02 draft handoff mismatch: $field"}
      }
      $handoffTemplate=Get-Content (Join-Path $Root 'planning/templates/HANDOFF_TEMPLATE.md') -Raw
      foreach($h in [regex]::Matches($handoffTemplate,'(?m)^## .+$')){if(-not $p01Handoff.Contains($h.Value.Trim())){throw 'P01 handoff missing template heading'}}
      if($p01Step -notmatch 'Awaiting user review'){throw 'Missing P01 human review gate'}
    }
  }else{throw 'Unsupported P01-S01 transition; user review required before closure'}
  }
  foreach($number in 1..4){
    $stepFile=Join-Path $Root ('planning/phase-00/steps/S{0:d2}.md' -f $number)
    $stepText=Get-Content $stepFile -Raw
    if($stepText -notmatch '(?m)^Status: Done\r?$'){throw "P00 step not Done: $number"}
  }
  foreach($name in @('S01-to-S02','S02-to-S03','S03-to-S04','S04-to-P01')){
    $readyText=Get-Content (Join-Path $Root "planning/phase-00/handoffs/$name.md") -Raw
    if($readyText -notmatch '(?m)^Status: Ready\r?$'){throw "P00 handoff not Ready: $name"}
  }
  $s04=Get-Content $s04Path -Raw
  foreach($field in @('Status: Done','Phase: P00','PreviousStep: P00-S03','NextStep: P01-S01')){
    if($s04 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "Closed S04 mismatch: $field"}
  }
  $phaseReview=Get-Content (Join-Path $Root 'planning/phase-00/PHASE_REVIEW.md') -Raw
  $phaseHandoff=Get-Content (Join-Path $Root 'planning/phase-00/handoffs/S04-to-P01.md') -Raw
  foreach($field in @('Phase: P00','Status: Approved','UserApproval: Approved','ApprovalDate: 2026-10-04','NextStep: P01-S01')){
    if($phaseReview -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "P00 phase review mismatch: $field"}
  }
  foreach($field in @('From: P00-S04','To: P01-S01','Status: Ready','UserApproval: Approved','ApprovalDate: 2026-10-04')){
    if($phaseHandoff -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "P01 handoff mismatch: $field"}
  }
  foreach($kind in @('STEP','HANDOFF')){
    $template=Get-Content (Join-Path $Root "planning/templates/${kind}_TEMPLATE.md") -Raw
    $actual=if($kind -eq 'STEP'){$s04}else{$phaseHandoff}
    foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $actual.Contains($h.Value.Trim())){throw "P00 closure missing template heading: $($h.Value)"}}
  }
  foreach($token in @('Definition of Done','نقطة واحدة موثوقة','قالب ثابت','Frontend','توافق','المتطلبات','P01-S01 NotStarted','.NET','Docker','V1','V2','V3','V4')){
    if(-not $phaseReview.Contains($token)){throw "P00 review missing closure evidence: $token"}
  }
  if($s04 -notmatch 'Passed؛ المستخدم راجع واعتمد' -or $phaseReview -notmatch 'المستخدم قال' -or $phaseHandoff -notmatch 'V4 Passed'){throw 'Missing P00 user approval evidence'}
}elseif($currentStatus -eq 'NotStarted'){
  if($state -notmatch '(?m)^- Phase: P00\r?$'){throw 'S04 initial phase mismatch'}
  if($state -notmatch '(?m)^- ActiveStepFile: None؛' -or (Test-Path $s04Path)){throw 'NotStarted S04 must have no active file'}
}elseif($currentStatus -in @('InProgress','InReview')){
  if($state -notmatch '(?m)^- Phase: P00\r?$'){throw 'S04 active phase mismatch'}
  if($state -notmatch '(?m)^- ActiveStepFile: planning/phase-00/steps/S04.md\r?$' -or -not (Test-Path $s04Path)){throw 'Missing S04 active file'}
  $s04=Get-Content $s04Path -Raw
  foreach($field in @("Status: $currentStatus",'Phase: P00','PreviousStep: P00-S03','NextStep: PhaseReview')){
    if($s04 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "S04 state mismatch: $field"}
  }
  $stepTemplate=Get-Content (Join-Path $Root 'planning/templates/STEP_TEMPLATE.md') -Raw
  foreach($h in [regex]::Matches($stepTemplate,'(?m)^## .+$')){if(-not $s04.Contains($h.Value.Trim())){throw 'S04 missing template heading'}}
  if($currentStatus -eq 'InReview'){
    $phaseHandoff=Get-Content (Join-Path $Root 'planning/phase-00/handoffs/S04-to-P01.md') -Raw
    foreach($field in @('Status: Draft','From: P00-S04','To: PhaseReview')){
      if($phaseHandoff -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "Phase handoff mismatch: $field"}
    }
    $handoffTemplate=Get-Content (Join-Path $Root 'planning/templates/HANDOFF_TEMPLATE.md') -Raw
    foreach($h in [regex]::Matches($handoffTemplate,'(?m)^## .+$')){if(-not $phaseHandoff.Contains($h.Value.Trim())){throw 'Phase handoff missing template heading'}}
    if($s04 -notmatch 'Awaiting user review'){throw 'Missing S04 human review gate'}
  }
}else{throw 'Unsupported S04 transition; phase approval must be checked before closure'}
if($map -notmatch '(?m)^Status: Approved\r?$' -or -not $map.Contains('مستند Word الأصلي، الذي لم يُعدّل')){throw 'Missing approved roadmap/source timing correction notice'}
if($s03 -notmatch 'V4 Approved' -or $s03 -notmatch 'المستخدم راجع التقرير واعتمد' -or $handoff -notmatch 'V4 Passed'){throw 'Missing human approval evidence'}
$decisions=Get-Content (Join-Path $Root 'docs/DECISIONS.md') -Raw
foreach($key in 1..8){if(-not $decisions.Contains(('D{0:d3}' -f $key))){throw 'Missing approved decision'}}
$foundations=@{1='FoundationStep: P02-S04';2='FoundationStep: P02-S04';3='FoundationStep: P04-S02';4='FoundationStep: P04-S03';5='FoundationPhase: P01';6='FoundationStep: P02-S01'}
$completion=@{1='CompletionStep: P08-S03';2='CompletionStep: P08-S01, P08-S02, P08-S03';3='CompletionStep: P08-S04';4='CompletionStep: P05-S05, P05-S06';5='CompletionStep: P08-S09'}
foreach($key in 1..6){
  $id=('Q003-{0:d2}' -f $key)
  $decisionBlock=[regex]::Match($decisions,'(?ms)^### '+$id+' .*?(?=^### |^## |\z)').Value
  if($decisionBlock -notmatch '(?m)^Status: Approved\r?$' -or -not $decisionBlock.Contains($foundations[$key])){throw "Unapproved or wrongly located foundation: $id"}
  if($completion.ContainsKey($key) -and -not $decisionBlock.Contains($completion[$key])){throw "Completion mismatch: $id"}
  if(-not $map.Contains($id)){throw "Missing roadmap decision: $id"}
}
if($decisions -match 'Pending user decision' -or $map -match 'كل اقتراح Pending|بعد اعتماده|قرار توزيع التأسيس مطلوب'){throw 'Stale pending approval in current decisions/roadmap'}
$required=@{
 'P01-S04'=@('Q003-05 Approved','Logging','Correlation ID','حجب الأسرار');
 'P01-S07'=@('Q003-05 Approved','كلمات المرور','OTP','Tokens');
 'P02-S01'=@('Q003-06 Approved','DbContext','Schemas','UoW','D003/D004');
 'P02-S04'=@('Q003-01/Q003-02 Approved','SQL storage','Worker','Outbox','نفس Transaction','Retry','التكرار','rollback','إعادة التشغيل','لا يضمن عدم تكرار Email عند فشل الشبكة','حدود ضمان التسليم');
 'P04-S02'=@('Q003-03 Approved','الفاعل','معنى العملية','السبب','الوقت','ذري','حجب الأسرار');
 'P04-S03'=@('Q003-04 Approved','IFileStorage','محلي آمن','اللوجو','التحقق من الملفات','استمرار التخزين','restart','path safety');
 'P05-S04'=@('Q003-04 Approved','بيانات اختبار','رحلة الرفع والنشر كاملة');
 'P05-S05'=@('Q003-04 Approved','P04-S03','توسيع معالجة صور المنتجات');
 'P05-S06'=@('Q003-04 Approved','P04-S03','رحلة الرفع والنشر كاملة');
 'P07-S06'=@('Q003-01/Q003-02 Approved','P02-S04','حدود ضمان البريد');
 'P08-S01'=@('Q003-02 Approved','استكمال','P02-S04');
 'P08-S02'=@('Q003-02 Approved','استكمال','P02-S04');
 'P08-S03'=@('Q003-01/Q003-02 Approved','استكمال','التعافي','المراقبة');
 'P08-S04'=@('Q003-03 Approved','استكمال','P04-S02','Service/Interceptor','القيم قبل وبعد');
 'P08-S09'=@('Q003-05 Approved','استكمال','P01','حجب الأسرار')
}
foreach($id in $required.Keys){
  $block=[regex]::Match($map,'(?ms)^### '+[regex]::Escape($id)+' .*?(?=^### |\z)').Value
  foreach($token in $required[$id]){if(-not $block.Contains($token)){throw "Missing approved service requirement in ${id}: $token"}}
}
foreach($token in @('لا يضمن عدم تكرار Email عند فشل الشبكة','exactly-once','فقد الاستجابة','صندوق المستلم')){if(-not $decisions.Contains($token)){throw "Missing email guarantee limit: $token"}}
Write-Output "PASS: 76 source rows match IDs/order/names/outputs/basic gates; 75 dependency edges; no missing nodes, forward edges or cycles."
Write-Output "PASS: $links local links/anchors; referenced IDs; template headings; S02/S03 Done, S03-to-S04 Ready, current $currentStep $currentStatus with consistent active file, P00 closure and review gate."
Write-Output 'PASS: six Approved decisions with foundation/completion timing; affected service conditions; early secret redaction; email guarantee limits; original Word correction notice and recorded user approval.'
Write-Output 'LIMIT: documentation checks only; service runtime, Backend, Docker, SDK, mail delivery and GitHub push are not verified.'
