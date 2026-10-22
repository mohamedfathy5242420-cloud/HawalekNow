$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path $PSScriptRoot ('../tmp/s06-settings-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
$localScript = Join-Path $PSScriptRoot 'local.ps1'
$cases = @(
    @{Name='missing'; Content=$null; Expected='Missing local settings'},
    @{Name='malformed'; Content='MSSQL_SA_PASSWORD=bad value'; Expected='Invalid local settings'},
    @{Name='weak'; Content='MSSQL_SA_PASSWORD=onlylowercaseletters'; Expected='Invalid SQL password'},
    @{Name='placeholder'; Content='MSSQL_SA_PASSWORD=Replace_Example_123'; Expected='Invalid SQL password'}
)
foreach ($case in $cases) {
    $path = Join-Path $fixtureRoot $case.Name
    if ($null -ne $case.Content) { [IO.File]::WriteAllText($path,$case.Content) }
    $message = ''
    try { & $localScript Validate -EnvFile $path; throw 'Unexpected acceptance' }
    catch { $message = $_.Exception.Message }
    if (-not $message.Contains($case.Expected)) { throw "Unexpected validation behavior: $($case.Name)" }
    if ($case.Content -and $message.Contains($case.Content.Split('=',2)[1])) { throw 'Settings value leaked in error.' }
    Write-Output "PASS: $($case.Name) rejected with a clear message and no value disclosure."
}
$valid = Join-Path $fixtureRoot '.env'
& $localScript Init -EnvFile $valid
& $localScript Validate -EnvFile $valid
$before = [IO.File]::ReadAllText($valid)
try { & $localScript Init -EnvFile $valid; throw 'Unexpected overwrite' }
catch { if (-not $_.Exception.Message.Contains('already exist')) { throw } }
if ([IO.File]::ReadAllText($valid) -ne $before) { throw 'Existing settings changed.' }
Write-Output 'PASS: random valid settings accepted; Init refuses overwriting existing settings.'
Write-Output 'Fixtures retained under ignored tmp only; no real settings modified.'
