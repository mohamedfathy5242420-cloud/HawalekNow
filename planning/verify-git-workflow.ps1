param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$blocked=@('src/Api/bin/Debug/app.dll','src/Api/obj/project.assets.json','app/node_modules/example/index.js','app/.angular/cache/data','app/dist/index.html','.vs/settings','app/.vscode/settings.json','.env','.env.production','src/Api/.env.local','src/Api/appsettings.Local.json','secrets.json','certs/server.pfx','certs/server.key','.aws/credentials','docker-data/sql/file','volumes/sql/file','logs/server.log','tmp/report.json','artifacts/report.json','Mazen_Source/sample.pdf','extract_project_materials.py','project_materials_extracted.json','Hawalek_Now_Project_Questions_AR_Answered.docx')
$allowed=@('.env.example','.env.production.example','app/.env.example','package-lock.json','app/package-lock.json','yarn.lock','pnpm-lock.yaml','src/Api/packages.lock.json','src/Infrastructure/Persistence/Migrations/Initial.cs','Dockerfile','docker-compose.yml','compose.yaml','src/Api/Api.csproj','HawalekNow.sln','src/Api/appsettings.json','src/Api/appsettings.Development.json','Hawalek_Now_Approved_Requirements_AR.docx','Hawalek_Now_Backend_Master_Execution_Plan_AR.docx','planning/phase-00/handoffs/P00-S03-REVIEW-REPORT.md','docs/GIT_WORKFLOW.md','.github/pull_request_template.md')
function Assert-Ignore([string]$Path,[bool]$Expected){
  & git -c safe.directory=D:/HwalekNow -C $Root check-ignore --no-index -q -- $Path
  $result=$LASTEXITCODE
  if($result -notin @(0,1)){throw "Git check-ignore failed: $Path (exit $result)"}
  if(($result -eq 0) -ne $Expected){throw "Ignore mismatch: $Path; expected ignored=$Expected"}
}
foreach($path in $blocked){Assert-Ignore $path $true}
foreach($path in $allowed){Assert-Ignore $path $false}
$tracked=@(& git -c safe.directory=D:/HwalekNow -C $Root ls-files)
if($LASTEXITCODE -ne 0){throw 'Cannot inspect tracked files'}
foreach($path in $tracked){Assert-Ignore $path $false}
Write-Output "PASS: $($blocked.Count) ignored and $($allowed.Count) allowed path samples; $($tracked.Count) tracked files are not ignored. No sample files created."
