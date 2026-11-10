param([string]$Image = 'hawaleknow/api:p01-ci')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'api-smoke-contracts.ps1')
# Isolated liveness container: no mounts, SQL/mail connection or Compose operations; generated test-only runtime key.
$name = 'hawaleknow-p01-smoke-' + [guid]::NewGuid().ToString('N')
try {
    $p02SmokeKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $id = & docker run --detach --name $name --cpus=1 --memory=256m --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m --publish 127.0.0.1::8080 --env "Identity__IdentifierKey=$p02SmokeKey" --env 'ConnectionStrings__Application=Server=unused.invalid;Database=HawalekNow_HostContract;Integrated Security=True;Encrypt=True' $Image
    if ($LASTEXITCODE -ne 0) { throw 'Cannot start the task API container.' }
    $port = & docker inspect --format '{{(index (index .NetworkSettings.Ports "8080/tcp") 0).HostPort}}' $id
    if ($LASTEXITCODE -ne 0 -or $port -notmatch '^\d+$') { throw 'Cannot resolve the task container port.' }
    $base = "http://127.0.0.1:$port"
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            $health = Invoke-WebRequest "$base/api/v1/health/live" -Headers @{'X-Correlation-ID' = 'p01-docker-smoke'} -SkipHttpErrorCheck -TimeoutSec 2
            $ready = $true
            break
        } catch { Start-Sleep -Seconds 1 }
    }
    if (-not $ready -or $health.StatusCode -ne 200 -or (Get-ApiResponseText $health) -ne 'Healthy' -or $health.Headers['Content-Type'] -notmatch '^text/plain' -or ($health.Headers['X-Correlation-ID'] -join '') -ne 'p01-docker-smoke') {
        throw 'Docker liveness contract failed.'
    }
    foreach ($path in @('/scalar', '/scalar/scalar.js', '/openapi/v1.json', '/api/v1/health/ready')) {
        $response = Invoke-WebRequest ($base + $path) -Headers @{'X-Correlation-ID' = 'p01-docker-smoke'} -SkipHttpErrorCheck -TimeoutSec 5
        Assert-SafeMissingResponse $response 'p01-docker-smoke'
    }
    $user = & docker inspect --format '{{.Config.User}}' $id
    if ($LASTEXITCODE -ne 0 -or -not $user -or $user -eq '0' -or $user -eq 'root') { throw 'The API image must run as a non-root user.' }
    Write-Output 'PASS: isolated non-root/read-only Docker API; liveness, correlation, safe 404 and Production documentation gate. SQL/mail readiness and persistence not tested.'
} finally {
    # The generated name identifies only this invocation's container. Never touch existing services/volumes.
    & docker rm --force $name 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Task container cleanup failed: $name" }
}
