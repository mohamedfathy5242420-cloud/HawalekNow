param([string]$Filter = '', [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$p02Root = Split-Path $PSScriptRoot -Parent
$p02PriorConnection = [Environment]::GetEnvironmentVariable('HAWALEKNOW_TEST_SQL')
try {
    if (-not $p02PriorConnection) {
        $p02EnvPath = Join-Path $p02Root 'docker/.env'
        $p02LocalSettings = [IO.File]::ReadAllText($p02EnvPath)
        $p02Match = [regex]::Match($p02LocalSettings, '\AMSSQL_SA_PASSWORD=([A-Za-z0-9!_@#%+\-]{16,128})\r?\n?\z')
        if (-not $p02Match.Success) { throw 'Invalid local SQL settings; values suppressed.' }
        $env:HAWALEKNOW_TEST_SQL = 'Server=127.0.0.1,1433;Database=master;User ID=sa;Password=' + $p02Match.Groups[1].Value + ';Encrypt=True;TrustServerCertificate=True;Connect Timeout=15'
    }
    $p02TestArgs = @('test', (Join-Path $PSScriptRoot 'HawalekNow.slnx'), '--configuration', 'Release', '--no-restore', '--disable-build-servers', '-m:1')
    if ($NoBuild) { $p02TestArgs += '--no-build' }
    if ($Filter) { $p02TestArgs += @('--filter', $Filter) }
    & dotnet @p02TestArgs
    if ($LASTEXITCODE -ne 0) { throw 'P02 verification failed; inspect test names, never print connection strings or secrets.' }
} finally {
    [Environment]::SetEnvironmentVariable('HAWALEKNOW_TEST_SQL', $p02PriorConnection)
}
