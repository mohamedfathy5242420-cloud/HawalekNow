param([switch]$SkipSql)
$ErrorActionPreference = 'Stop'
$p02Root = Split-Path $PSScriptRoot -Parent
function Read-OwnedContainer([string]$Name, [string]$Label, [string]$Owner) {
    $p02Json = & docker inspect $Name 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Required owned container is missing; no resource was replaced.' }
    $p02Container = ($p02Json | ConvertFrom-Json)[0]
    if ($p02Container.Config.Labels.$Label -ne $Owner) { throw 'Container ownership mismatch.' }
    return $p02Container
}
if (-not $SkipSql) {
    $p02Sql = Read-OwnedContainer 'hawaleknow-local-sql-1' 'com.docker.compose.project' 'hawaleknow-local'
    if ($p02Sql.State.Status -ne 'running') {
        & docker start $p02Sql.Id | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Owned SQL startup failed.' }
    }
    for ($p02Attempt = 0; $p02Attempt -lt 20; $p02Attempt++) {
        $p02Sql = Read-OwnedContainer 'hawaleknow-local-sql-1' 'com.docker.compose.project' 'hawaleknow-local'
        if ($p02Sql.State.Health.Status -eq 'healthy') { break }
        Start-Sleep -Seconds 2
    }
    if ($p02Sql.State.Health.Status -ne 'healthy') { throw 'Owned SQL is not ready; rerun after recovery completes.' }
}
$p02ServicePath = Join-Path $p02Root 'tmp/p02-mailpit-service.json'
if (Test-Path -LiteralPath $p02ServicePath) {
    $p02Metadata = Get-Content -LiteralPath $p02ServicePath -Raw | ConvertFrom-Json
    if ($p02Metadata.name -notmatch '^hawaleknow-p02-mailpit-[a-f0-9]{32}$') { throw 'Invalid Mailpit ownership metadata.' }
    $p02Mail = Read-OwnedContainer $p02Metadata.name 'io.hawaleknow.owner' 'p02-mailpit'
    if ($p02Mail.Id -ne $p02Metadata.id) { throw 'Mailpit container identity changed; no resource was replaced.' }
} else {
    $p02Name = 'hawaleknow-p02-mailpit-' + [Guid]::NewGuid().ToString('N')
    $p02Image = 'axllent/mailpit:v1.31.6@sha256:23fe39e81bde596a918000b3135c0f85dc8bb4c9ea6be274b051143f3e300387'
    & docker create --name $p02Name --label io.hawaleknow.owner=p02-mailpit --publish 127.0.0.1::1025 --publish 127.0.0.1::8025 --env MP_DISABLE_VERSION_CHECK=true --env MP_BLOCK_REMOTE_CSS_AND_FONTS=true --env MP_SMTP_DISABLE_RDNS=true --env MP_ENABLE_SPAMASSASSIN=false $p02Image | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Isolated Mailpit creation failed.' }
    $p02Mail = Read-OwnedContainer $p02Name 'io.hawaleknow.owner' 'p02-mailpit'
}
if ($p02Mail.State.Status -ne 'running') {
    & docker start $p02Mail.Id | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Owned Mailpit startup failed.' }
}
$p02Mail = Read-OwnedContainer $p02Mail.Name.TrimStart('/') 'io.hawaleknow.owner' 'p02-mailpit'
$p02Metadata = [ordered]@{ name = $p02Mail.Name.TrimStart('/'); id = $p02Mail.Id; smtpPort = [int]$p02Mail.NetworkSettings.Ports.'1025/tcp'[0].HostPort; httpPort = [int]$p02Mail.NetworkSettings.Ports.'8025/tcp'[0].HostPort; owner = 'p02-mailpit' }
New-Item -ItemType Directory -Path (Split-Path $p02ServicePath -Parent) -Force | Out-Null
$p02Metadata | ConvertTo-Json | Set-Content -LiteralPath $p02ServicePath
Write-Output ([pscustomobject]$p02Metadata)
