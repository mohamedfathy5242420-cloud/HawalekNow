param(
    [ValidateSet('Init','Validate','Up','Status','Stop','Down','Restart','Recreate')]
    [string]$Action = 'Status',
    [string]$EnvFile = (Join-Path $PSScriptRoot '.env')
)
$ErrorActionPreference = 'Stop'
$composeFile = Join-Path $PSScriptRoot 'compose.yaml'
if ($Action -eq 'Init') {
    if (Test-Path -LiteralPath $EnvFile) { throw 'Local settings already exist; no overwrite performed.' }
    $password = 'Aa1!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    [IO.File]::WriteAllText($EnvFile, "MSSQL_SA_PASSWORD=$password`n", [Text.UTF8Encoding]::new($false))
    Write-Output 'Created ignored local settings with a random development password. Value not displayed.'
    return
}
if (-not (Test-Path -LiteralPath $EnvFile -PathType Leaf)) {
    throw 'Missing local settings. Run ./docker/local.ps1 Init first (or configure docker/.env from the example).'
}
$settings = [IO.File]::ReadAllText($EnvFile)
if ($settings -notmatch '\AMSSQL_SA_PASSWORD=([A-Za-z0-9!_@#%+\-]{16,128})\r?\n?\z') {
    throw 'Invalid local settings: require one MSSQL_SA_PASSWORD line, 16-128 allowed characters; no placeholders, spaces or interpolation.'
}
$password = $Matches[1]
if ($password -notmatch '[A-Z]' -or $password -notmatch '[a-z]' -or $password -notmatch '[0-9]' -or $password -notmatch '[!_@#%+\-]' -or $password -match '(?i)replace|change|example|placeholder') {
    throw 'Invalid SQL password: use upper/lowercase, number and symbol; replace example placeholders. Value not displayed.'
}
if (Test-Path Env:MSSQL_SA_PASSWORD) { throw 'Remove process MSSQL_SA_PASSWORD override; this wrapper uses the local file only.' }
$composeArgs = @('compose','--env-file',$EnvFile,'-f',$composeFile)
# config --quiet validates without exposing interpolated secrets.
$diagnostic = & docker @composeArgs config --quiet 2>&1
if ($LASTEXITCODE -ne 0) { throw 'Compose validation failed. Check Docker availability and compose.yaml. Details suppressed to protect settings.' }
if ($Action -eq 'Validate') { Write-Output 'PASS: local settings and Compose syntax (no rendered configuration printed).'; return }
$operation = switch ($Action) {
    'Up'       { @('up','-d','--build','--wait','--wait-timeout','240') }
    'Status'   { @('ps') }
    'Stop'     { @('stop','--timeout','60') }
    'Down'     { @('down','--timeout','60') }
    'Restart'  { @('restart','--timeout','60') }
    'Recreate' { @('up','-d','--force-recreate','--wait','--wait-timeout','240') }
}
& docker @composeArgs @operation
if ($LASTEXITCODE -ne 0) { throw "Compose $Action failed; no cleanup performed." }
