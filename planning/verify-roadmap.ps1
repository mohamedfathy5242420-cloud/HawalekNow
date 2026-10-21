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
if(Test-Path (Join-Path $Root 'backend/API_ERRORS.md')){$files += 'backend/API_ERRORS.md'}
foreach($candidate in @('README.md','backend/README.md','docker/README.md','planning/phase-01/steps/S01.md','planning/phase-01/handoffs/S01-to-S02.md','planning/phase-01/steps/S02.md','planning/phase-01/handoffs/S02-to-S03.md','planning/phase-01/steps/S03.md','planning/phase-01/handoffs/S03-to-S04.md','backend/MODULES.md','backend/PACKAGES.md','planning/phase-01/steps/S04.md','planning/phase-01/handoffs/S04-to-S05.md','planning/phase-01/steps/S05.md','planning/phase-01/handoffs/S05-to-S06.md','planning/phase-01/steps/S06.md','planning/phase-01/handoffs/S06-to-S07.md')){
  if(Test-Path (Join-Path $Root $candidate)){$files += $candidate}
}
$moduleRoot=Join-Path $Root 'backend/src'
if(Test-Path $moduleRoot){
  $files += @(Get-ChildItem -LiteralPath $moduleRoot -Filter README.md -Recurse -File |
    Where-Object {$_.FullName -match '[\\/]Modules[\\/]' -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]'} |
    ForEach-Object {[IO.Path]::GetRelativePath($Root,$_.FullName)})
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
if($s02 -notmatch '(?m)^Status: Done\r?$' -or $s03 -notmatch '(?m)^Status: Done\r?$' -or $state -notmatch '(?m)^- Step: (P00-S04|P01-S01|P01-S02|P01-S03|P01-S04|P01-S05|P01-S06)\r?$'){throw 'Closure state mismatch'}
$handoff=Get-Content (Join-Path $Root 'planning/phase-00/handoffs/S03-to-S04.md') -Raw
if($handoff -notmatch '(?m)^Status: Ready\r?$' -or $handoff -notmatch '(?m)^From: P00-S03\r?$' -or $handoff -notmatch '(?m)^To: P00-S04\r?$'){throw 'Closure handoff mismatch'}
$currentStatus=[regex]::Match($state,'(?m)^- Status: (\w+)\r?$').Groups[1].Value
$s04Path=Join-Path $Root 'planning/phase-00/steps/S04.md'
$currentStep=[regex]::Match($state,'(?m)^- Step: (P\d{2}-S\d{2})\r?$').Groups[1].Value
if($currentStep -in @('P01-S01','P01-S02','P01-S03','P01-S04','P01-S05','P01-S06')){
  if($state -notmatch '(?m)^- Phase: P01\r?$'){throw 'P01 phase mismatch'}
  if($currentStep -in @('P01-S02','P01-S03','P01-S04','P01-S05','P01-S06')){
    if($currentStep -in @('P01-S03','P01-S04','P01-S05','P01-S06')){
      $activeS03Path=Join-Path $Root 'planning/phase-01/steps/S03.md'
      if($currentStep -in @('P01-S04','P01-S05','P01-S06')){
        $activeS04Path=Join-Path $Root 'planning/phase-01/steps/S04.md'
        if($currentStep -in @('P01-S05','P01-S06')){
          $activeS05Path=Join-Path $Root 'planning/phase-01/steps/S05.md'
          if($currentStep -eq 'P01-S06'){
            $activeS06Path=Join-Path $Root 'planning/phase-01/steps/S06.md'
            if($currentStatus -eq 'NotStarted'){
              if($state -notmatch '(?m)^- ActiveStepFile: None؛' -or (Test-Path $activeS06Path)){throw 'NotStarted S06 must have no execution file'}
            }elseif($currentStatus -in @('InProgress','InReview')){
              if($state -notmatch '(?m)^- ActiveStepFile: planning/phase-01/steps/S06.md\r?$'){throw 'Missing S06 active file'}
              $activeS06=Get-Content $activeS06Path -Raw
              foreach($field in @("Status: $currentStatus",'Phase: P01','PreviousStep: P01-S05','NextStep: P01-S07','UserApproval: Pending','ReviewStatus: Pending')){
                $name=$field.Split(':',2)[0]
                $values=[regex]::Matches($activeS06,'(?m)^'+[regex]::Escape($name)+': [^\r\n]+\r?$')
                if($values.Count -ne 1 -or $values[0].Value.TrimEnd("`r") -ne $field){throw "S06 field mismatch: $field"}
              }
              $template=Get-Content (Join-Path $Root 'planning/templates/STEP_TEMPLATE.md') -Raw
              foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $activeS06.Contains($h.Value.Trim())){throw 'S06 template heading missing'}}
              foreach($token in @('69a244f25381c4e6a47c2e0086392d7430580341','D009','Full Text','Mailpit','Awaiting user review','TARGET_SCHEDULE.md')){
                if(-not $activeS06.Contains($token)){throw "S06 evidence missing: $token"}
              }
              if(Test-Path (Join-Path $Root 'planning/phase-01/steps/S07.md')){throw 'S07 must not start before S06 approval'}
              if($currentStatus -eq 'InReview'){
                foreach($field in @('ComposeVerification: Passed','SqlVerification: Passed','FullTextVerification: Passed','MailpitVerification: Passed','HealthVerification: Passed','PersistenceVerification: Passed')){
                  if($activeS06 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "S06 verification missing: $field"}
                }
                $decisionS06=Get-Content (Join-Path $Root 'docs/DECISIONS.md') -Raw
                $decisionS06=[regex]::Match($decisionS06,'(?ms)^## D009 .*?(?=^## |\z)').Value
                foreach($token in @('VerificationStep: P01-S06','EngineVerification: Passed','SqlVerification: Passed','FullTextVerification: Passed','MailpitVerification: Passed','HealthVerification: Passed','PersistenceVerification: Passed')){
                  if(-not $decisionS06.Contains($token)){throw "D009 actual verification missing: $token"}
                }
                $next=Get-Content (Join-Path $Root 'planning/phase-01/handoffs/S06-to-S07.md') -Raw
                foreach($field in @('Status: Draft','From: P01-S06','To: P01-S07','UserApproval: Pending')){
                  $name=$field.Split(':',2)[0]
                  $values=[regex]::Matches($next,'(?m)^'+[regex]::Escape($name)+': [^\r\n]+\r?$')
                  if($values.Count -ne 1 -or $values[0].Value.TrimEnd("`r") -ne $field){throw "S07 draft mismatch: $field"}
                }
                $template=Get-Content (Join-Path $Root 'planning/templates/HANDOFF_TEMPLATE.md') -Raw
                foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $next.Contains($h.Value.Trim())){throw 'S07 handoff heading missing'}}
                foreach($file in @('docker/compose.yaml','docker/local.ps1','docker/verify-services.ps1','docker/.env.example','docker/sql/Dockerfile')){
                  if(-not (Test-Path (Join-Path $Root $file))){throw "S06 artifact missing: $file"}
                }
              }
            }else{throw 'Unsupported S06 status; user review required'}
            $closedS05=Get-Content $activeS05Path -Raw
            $readyS06=Get-Content (Join-Path $Root 'planning/phase-01/handoffs/S05-to-S06.md') -Raw
            foreach($kind in @('STEP','HANDOFF')){
              $actual=if($kind -eq 'STEP'){$closedS05}else{$readyS06}
              $fields=@('UserApproval: Approved','ReviewStatus: Approved','ApprovalDate: 2026-10-21',
                'ReviewedHead: 9b0150669b718a0012e7413fc5bcb4e78f2e1ba9',
                'LoggingPolicy: PipelineOnly; HostLogsBlocked; OperationalDiagnosticsIncomplete',
                'DockerVerification: Deferred; D009; NotRun')
              $fields+=if($kind -eq 'STEP'){@('Status: Done','Phase: P01','PreviousStep: P01-S04','NextStep: P01-S06')}else{@('Status: Ready','From: P01-S05','To: P01-S06')}
              foreach($field in $fields){
                $name=$field.Split(':',2)[0]
                $matches=[regex]::Matches($actual,'(?m)^'+[regex]::Escape($name)+': [^\r\n]+\r?$')
                if($matches.Count -ne 1 -or $matches[0].Value.TrimEnd("`r") -ne $field){throw "S05 closure $kind mismatch: $field"}
              }
              $template=Get-Content (Join-Path $Root "planning/templates/${kind}_TEMPLATE.md") -Raw
              foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $actual.Contains($h.Value.Trim())){throw 'S05 closure missing template heading'}}
              foreach($token in @('D002','D004','Q003-05','D009','12.5.0','12.1.1','Success(null)',
                'application/json','204','preserveStaticLogger: true','V4 Approved','ليس Passed')){
                if(-not $actual.Contains($token)){throw "Missing S05 closure evidence: $token"}
              }
            }
            if(-not $state.Contains('LoggingPolicy: PipelineOnly; HostLogsBlocked; OperationalDiagnosticsIncomplete')){throw 'Missing current logging limit'}
            $s05ClosureBlock=[regex]::Match($map,'(?ms)^### P01-S05 .*?(?=^### |\z)').Value
            foreach($token in @('Status: Done','UserApproval: Approved','ReviewStatus: Approved','ApprovalDate: 2026-10-21',
              'ReviewedHead: 9b0150669b718a0012e7413fc5bcb4e78f2e1ba9')){
              if(-not $s05ClosureBlock.Contains($token)){throw "S05 roadmap closure mismatch: $token"}
            }
            $s06Block=[regex]::Match($map,'(?ms)^### P01-S06 .*?(?=^### |\z)').Value
            if(-not $s06Block.Contains("Status: $currentStatus")){throw 'S06 roadmap state mismatch'}
          }elseif($currentStatus -eq 'NotStarted'){
            if($state -notmatch '(?m)^- ActiveStepFile: None؛' -or (Test-Path $activeS05Path)){throw 'NotStarted S05 must have no execution file'}
          }elseif($currentStatus -in @('InProgress','InReview')){
            if($state -notmatch '(?m)^- ActiveStepFile: planning/phase-01/steps/S05.md\r?$'){throw 'Missing S05 active file'}
            $activeS05=Get-Content $activeS05Path -Raw
            foreach($field in @("Status: $currentStatus",'Phase: P01','PreviousStep: P01-S04','NextStep: P01-S06','LoggingPolicy: PipelineOnly; HostLogsBlocked; OperationalDiagnosticsIncomplete')){
              if($activeS05 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "S05 state mismatch: $field"}
            }
            $template=Get-Content (Join-Path $Root 'planning/templates/STEP_TEMPLATE.md') -Raw
            foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $activeS05.Contains($h.Value.Trim())){throw 'S05 missing template heading'}}
            foreach($token in @('D002','D004','Q003-05','D009','D010','D011','12.5.0','12.1.1','Result','ProblemDetails','TestServer','Correlation','ليس Passed','Awaiting user review','4ff304825aef95728cba293c79b32523ebbb71ea')){
              if(-not $activeS05.Contains($token)){throw "Missing S05 requirement: $token"}
            }
            if($activeS05 -match '(?m)^UserApproval: Approved\r?$' -or (Test-Path (Join-Path $Root 'planning/phase-01/steps/S06.md'))){throw 'S05 needs user review before closure or S06 execution'}
            if($currentStatus -eq 'InReview'){
              $draftS06=Get-Content (Join-Path $Root 'planning/phase-01/handoffs/S05-to-S06.md') -Raw
              foreach($field in @('From: P01-S05','To: P01-S06','Status: Draft','LoggingPolicy: PipelineOnly; HostLogsBlocked; OperationalDiagnosticsIncomplete')){
                if($draftS06 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "S06 draft mismatch: $field"}
              }
              $template=Get-Content (Join-Path $Root 'planning/templates/HANDOFF_TEMPLATE.md') -Raw
              foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $draftS06.Contains($h.Value.Trim())){throw 'S06 handoff missing template heading'}}
              if($draftS06 -match '(?m)^UserApproval: Approved\r?$'){throw 'S06 handoff cannot claim user approval'}
              foreach($token in @('D002','D004','Q003-05','D009','ليس Passed','Awaiting user review')){if(-not $draftS06.Contains($token)){throw "Missing S06 limit: $token"}}
            }
          }else{throw 'Unsupported S05 status; user approval required before closure'}
          $closedS04=Get-Content $activeS04Path -Raw
          $readyS05=Get-Content (Join-Path $Root 'planning/phase-01/handoffs/S04-to-S05.md') -Raw
          foreach($field in @('Status: Done','UserApproval: Approved','ApprovalDate: 2026-10-13','ReviewedHead: da151d66b5c7c98ed0437d2e05899fcb8a3ed786','Phase: P01','PreviousStep: P01-S03','NextStep: P01-S05')){
            if($closedS04 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "Closed P01-S04 mismatch: $field"}
          }
          foreach($field in @('Status: Ready','UserApproval: Approved','ApprovalDate: 2026-10-13','From: P01-S04','To: P01-S05')){
            if($readyS05 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "Ready P01-S05 handoff mismatch: $field"}
          }
          foreach($kind in @('STEP','HANDOFF')){
            $actual=if($kind -eq 'STEP'){$closedS04}else{$readyS05}
            $template=Get-Content (Join-Path $Root "planning/templates/${kind}_TEMPLATE.md") -Raw
            foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $actual.Contains($h.Value.Trim())){throw 'P01-S04 closure missing template heading'}}
            foreach($token in @('D002','D004','Q003-05','D009','12.5.0','12.1.1','ليس Passed','P01-S06','V4 Approved','LoggingPolicy: PipelineOnly; HostLogsBlocked; OperationalDiagnosticsIncomplete')){
              if(-not $actual.Contains($token)){throw "Missing S04 closure evidence: $token"}
            }
          }
          if(-not $state.Contains('LoggingPolicy: PipelineOnly; HostLogsBlocked; OperationalDiagnosticsIncomplete')){throw 'Missing current logging limit'}
          $s05Block=[regex]::Match($map,'(?ms)^### P01-S05 .*?(?=^### |\z)').Value
          if(-not $s05Block.Contains("Status: $(if($currentStep -eq 'P01-S06'){'Done'}else{$currentStatus})")){throw 'S05 roadmap state mismatch'}
        }elseif($currentStatus -eq 'NotStarted'){
          if($state -notmatch '(?m)^- ActiveStepFile: None؛' -or (Test-Path $activeS04Path)){throw 'NotStarted S04 must have no execution file'}
        }elseif($currentStatus -in @('InProgress','InReview')){
          if($state -notmatch '(?m)^- ActiveStepFile: planning/phase-01/steps/S04.md\r?$'){throw 'Missing S04 active file'}
          $activeS04=Get-Content $activeS04Path -Raw
          foreach($field in @("Status: $currentStatus",'Phase: P01','PreviousStep: P01-S03','NextStep: P01-S05')){
            if($activeS04 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "S04 state mismatch: $field"}
          }
          $template=Get-Content (Join-Path $Root 'planning/templates/STEP_TEMPLATE.md') -Raw
          foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $activeS04.Contains($h.Value.Trim())){throw 'S04 missing template heading'}}
          foreach($token in @('D002','D004','Q003-05','D010','D011','12.5.0','Correlation ID','Docker','P01-S06','ليس Passed')){
            if(-not $activeS04.Contains($token)){throw "Missing S04 requirement: $token"}
          }
          if($activeS04 -match '(?m)^UserApproval: Approved\r?$'){throw 'S04 cannot claim final user approval'}
          if(Test-Path (Join-Path $Root 'planning/phase-01/steps/S05.md')){throw 'S05 must not start during S04'}
          if($currentStatus -eq 'InReview'){
            $draftS05=Get-Content (Join-Path $Root 'planning/phase-01/handoffs/S04-to-S05.md') -Raw
            foreach($field in @('Status: Draft','From: P01-S04','To: P01-S05')){
              if($draftS05 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "S05 draft mismatch: $field"}
            }
            $template=Get-Content (Join-Path $Root 'planning/templates/HANDOFF_TEMPLATE.md') -Raw
            foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $draftS05.Contains($h.Value.Trim())){throw 'S05 missing handoff heading'}}
            if($draftS05 -match '(?m)^UserApproval: Approved\r?$' -or -not $activeS04.Contains('Awaiting user review')){throw 'Missing S04 user review gate'}
            foreach($token in @('D004','Q003-05','D009','ليس Passed')){if(-not $draftS05.Contains($token)){throw "Missing S05 preserved limit: $token"}}
          }
        }else{throw 'Unsupported S04 state; user approval required before closure'}
        $closedS03=Get-Content $activeS03Path -Raw
        $readyS04=Get-Content (Join-Path $Root 'planning/phase-01/handoffs/S03-to-S04.md') -Raw
        foreach($field in @('Status: Done','UserApproval: Approved','ApprovalDate: 2026-10-11','Phase: P01','PreviousStep: P01-S02','NextStep: P01-S04')){
          if($closedS03 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "Closed P01-S03 mismatch: $field"}
        }
        foreach($field in @('Status: Ready','From: P01-S03','To: P01-S04','UserApproval: Approved','ApprovalDate: 2026-10-11')){
          if($readyS04 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "Ready P01-S04 handoff mismatch: $field"}
        }
        foreach($kind in @('STEP','HANDOFF')){
          $template=Get-Content (Join-Path $Root "planning/templates/${kind}_TEMPLATE.md") -Raw
          $actual=if($kind -eq 'STEP'){$closedS03}else{$readyS04}
          foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $actual.Contains($h.Value.Trim())){throw 'P01-S03 closure missing template heading'}}
          foreach($token in @('D010','D011','Docker','P01-S06','ليس Passed','V4 Approved')){
            if(-not $actual.Contains($token)){throw "Missing S03 closure evidence: $token"}
          }
        }
        $decisionText=Get-Content (Join-Path $Root 'docs/DECISIONS.md') -Raw
        foreach($id in @('D010','D011')){
          $decisionBlock=[regex]::Match($decisionText,'(?ms)^## '+$id+' .*?(?=^## |\z)').Value
          foreach($field in @('Status: Approved','ApprovalDate: 2026-10-11','OwnerStep: P01-S03')){
            if($decisionBlock -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "S03 closure decision mismatch: $id $field"}
          }
        }
        $moduleDoc=Get-Content (Join-Path $Root 'backend/MODULES.md') -Raw
        foreach($module in @('Identity','Merchants','Catalog','Locations','Search','Favorites','Reports','Notifications','Analytics')){
          if(-not $moduleDoc.Contains('| '+$module+' |')){throw "Missing documented module: $module"}
        }
        foreach($id in @('D010','D011')){if(-not $moduleDoc.Contains($id)){throw "Missing module ownership decision: $id"}}
        if($currentStep -in @('P01-S05','P01-S06')){
          $closureBlock=[regex]::Match($map,'(?ms)^### P01-S04 .*?(?=^### |\z)').Value
          foreach($token in @('UserApproval: Approved','ApprovalDate: 2026-10-13')){if(-not $closureBlock.Contains($token)){throw 'Missing S04 roadmap approval'}}
        }
        $s04Block=[regex]::Match($map,'(?ms)^### P01-S04 .*?(?=^### |\z)').Value
        if(-not $s04Block.Contains("Status: $(if($currentStep -in @('P01-S05','P01-S06')){'Done'}else{$currentStatus})")){throw 'S04 roadmap state mismatch'}
      }elseif($currentStatus -eq 'NotStarted'){
        if($state -notmatch '(?m)^- ActiveStepFile: None؛' -or (Test-Path $activeS03Path)){throw 'NotStarted P01-S03 must have no execution file'}
      }elseif($currentStatus -in @('InProgress','InReview')){
        if($state -notmatch '(?m)^- ActiveStepFile: planning/phase-01/steps/S03.md\r?$'){throw 'Missing P01-S03 active file'}
        $activeS03=Get-Content $activeS03Path -Raw
        foreach($field in @("Status: $currentStatus",'Phase: P01','PreviousStep: P01-S02','NextStep: P01-S04')){
          if($activeS03 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "P01-S03 state mismatch: $field"}
        }
        $template=Get-Content (Join-Path $Root 'planning/templates/STEP_TEMPLATE.md') -Raw
        foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $activeS03.Contains($h.Value.Trim())){throw 'P01-S03 missing template heading'}}
        if(-not $activeS03.Contains('Docker') -or -not $activeS03.Contains('P01-S06') -or -not $activeS03.Contains('ليس Passed')){throw 'Missing S03 Docker deferral'}
        if(Test-Path (Join-Path $Root 'planning/phase-01/steps/S04.md')){throw 'P01-S04 must not start during S03'}
        if($activeS03 -match '(?m)^UserApproval: Approved\r?$'){throw 'P01-S03 review cannot claim final user approval'}
        $decisionText=Get-Content (Join-Path $Root 'docs/DECISIONS.md') -Raw
        foreach($id in @('D010','D011')){
          $decisionBlock=[regex]::Match($decisionText,'(?ms)^## '+$id+' .*?(?=^## |\z)').Value
          foreach($field in @('Status: Approved','ApprovalDate: 2026-10-11','OwnerStep: P01-S03')){
            if($decisionBlock -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "S03 module decision mismatch: $id $field"}
          }
          if(-not $activeS03.Contains($id)){throw "Missing S03 decision reference: $id"}
        }
        if($currentStatus -eq 'InReview'){
          $draftS04=Get-Content (Join-Path $Root 'planning/phase-01/handoffs/S03-to-S04.md') -Raw
          foreach($field in @('Status: Draft','From: P01-S03','To: P01-S04')){
            if($draftS04 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "P01-S04 draft handoff mismatch: $field"}
          }
          $template=Get-Content (Join-Path $Root 'planning/templates/HANDOFF_TEMPLATE.md') -Raw
          foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $draftS04.Contains($h.Value.Trim())){throw 'P01-S04 handoff missing template heading'}}
          if(-not $activeS03.Contains('Awaiting user review')){throw 'Missing P01-S03 human review gate'}
          if(-not $draftS04.Contains('D010') -or -not $draftS04.Contains('D011') -or -not $draftS04.Contains('ليس Passed')){throw 'Missing S04 decisions or Docker limit'}
          $moduleDoc=Get-Content (Join-Path $Root 'backend/MODULES.md') -Raw
          foreach($module in @('Identity','Merchants','Catalog','Locations','Search','Favorites','Reports','Notifications','Analytics')){
            if(-not $moduleDoc.Contains('| '+$module+' |')){throw "Missing documented module: $module"}
          }
          foreach($id in @('D010','D011')){if(-not $moduleDoc.Contains($id)){throw "Missing module ownership decision: $id"}}
        }
      }else{throw 'Unsupported P01-S03 status; user review required before closure'}
      $approvedS02=Get-Content (Join-Path $Root 'planning/phase-01/steps/S02.md') -Raw
      $readyS03=Get-Content (Join-Path $Root 'planning/phase-01/handoffs/S02-to-S03.md') -Raw
      foreach($field in @('Status: Done','UserApproval: Approved','ApprovalDate: 2026-10-06','Phase: P01','PreviousStep: P01-S01','NextStep: P01-S03')){
        if($approvedS02 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "Closed P01-S02 mismatch: $field"}
      }
      foreach($field in @('Status: Ready','From: P01-S02','To: P01-S03','UserApproval: Approved','ApprovalDate: 2026-10-06')){
        if($readyS03 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "Ready P01-S03 handoff mismatch: $field"}
      }
      foreach($kind in @('STEP','HANDOFF')){
        $template=Get-Content (Join-Path $Root "planning/templates/${kind}_TEMPLATE.md") -Raw
        $actual=if($kind -eq 'STEP'){$approvedS02}else{$readyS03}
        foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $actual.Contains($h.Value.Trim())){throw 'P01-S02 closure missing template heading'}}
        if(-not $actual.Contains('Docker') -or -not $actual.Contains('P01-S06') -or -not $actual.Contains('ليس Passed')){throw 'Missing S02 Docker deferral'}
      }
      if(-not $approvedS02.Contains('V4 Approved') -or -not $readyS03.Contains('V4 Approved')){throw 'Missing S02 user review evidence'}
      $s02Block=[regex]::Match($map,'(?ms)^### P01-S02 .*?(?=^### |\z)').Value
      if(-not $s02Block.Contains('Status: Done') -or -not $s02Block.Contains('2026-10-06')){throw 'Missing S02 roadmap closure'}
      $s03Block=[regex]::Match($map,'(?ms)^### P01-S03 .*?(?=^### |\z)').Value
      $expectedS03Status=if($currentStep -in @('P01-S04','P01-S05','P01-S06')){'Done'}else{$currentStatus}
      if(-not $s03Block.Contains("Status: $expectedS03Status")){throw 'S03 roadmap state mismatch'}
      if($currentStep -in @('P01-S04','P01-S05','P01-S06') -and (-not $s03Block.Contains('UserApproval: Approved') -or -not $s03Block.Contains('ApprovalDate: 2026-10-11'))){throw 'Missing S03 roadmap approval'}
    }else{
    if($currentStatus -eq 'NotStarted'){
      if($state -notmatch '(?m)^- ActiveStepFile: None؛' -or (Test-Path (Join-Path $Root 'planning/phase-01/steps/S02.md'))){throw 'P01-S02 NotStarted must have no active file'}
    }elseif($currentStatus -in @('InProgress','InReview')){
      if($state -notmatch '(?m)^- ActiveStepFile: planning/phase-01/steps/S02.md\r?$'){throw 'Missing P01-S02 active file'}
      $activeS02=Get-Content (Join-Path $Root 'planning/phase-01/steps/S02.md') -Raw
      foreach($field in @("Status: $currentStatus",'Phase: P01','PreviousStep: P01-S01','NextStep: P01-S03')){
        if($activeS02 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "P01-S02 state mismatch: $field"}
      }
      $template=Get-Content (Join-Path $Root 'planning/templates/STEP_TEMPLATE.md') -Raw
      foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $activeS02.Contains($h.Value.Trim())){throw 'P01-S02 missing template heading'}}
      if(Test-Path (Join-Path $Root 'planning/phase-01/steps/S03.md')){throw 'P01-S03 must not start during S02'}
      if($currentStatus -eq 'InReview'){
        $draftS03=Get-Content (Join-Path $Root 'planning/phase-01/handoffs/S02-to-S03.md') -Raw
        foreach($field in @('Status: Draft','From: P01-S02','To: P01-S03')){
          if($draftS03 -notmatch ('(?m)^'+[regex]::Escape($field)+'\r?$')){throw "P01-S03 handoff mismatch: $field"}
        }
        $template=Get-Content (Join-Path $Root 'planning/templates/HANDOFF_TEMPLATE.md') -Raw
        foreach($h in [regex]::Matches($template,'(?m)^## .+$')){if(-not $draftS03.Contains($h.Value.Trim())){throw 'P01-S03 handoff missing template heading'}}
        if(-not $activeS02.Contains('Awaiting user review')){throw 'Missing P01-S02 human review gate'}
      }
    }else{throw 'Unsupported P01-S02 status; review required before closure'}
    }
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
    $dockerTiming=if($currentStep -eq 'P01-S06' -and $currentStatus -in @('InProgress','InReview')){'VerificationStep: P01-S06'}else{'DeferredTo: P01-S06'}
    foreach($token in @('Status: Approved',$dockerTiming,'ليس Passed')){if(-not $dockerDecision.Contains($token)){throw "Missing approved Docker timing/history: $token"}}
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
Write-Output "PASS: $links local links/anchors; referenced IDs; template headings; P00-S02/P00-S03 Done, P00-S03-to-S04 Ready, current $currentStep $currentStatus with consistent active file, P00 closure and review gate."
if($currentStep -eq 'P01-S03' -and $currentStatus -in @('InProgress','InReview')){
  Write-Output 'PASS: S02 closure preserved; D010/D011 Approved and linked; S03 active file and review gate; S04 has no execution file.'
}
if($currentStep -in @('P01-S04','P01-S05','P01-S06')){
  Write-Output 'PASS: S03 Done and user-approved, S03-to-S04 Ready; D010/D011 preserved; S04 state and execution/review gate checked; earlier closure gates preserved.'
}
Write-Output 'PASS: six Approved decisions with foundation/completion timing; affected service conditions; early secret redaction; email guarantee limits; original Word correction notice and recorded user approval.'
Write-Output 'LIMIT: documentation checks only; service runtime, Backend, Docker, SDK, mail delivery and GitHub push are not verified.'
