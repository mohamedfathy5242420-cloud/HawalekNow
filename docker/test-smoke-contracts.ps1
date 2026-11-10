$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'api-smoke-contracts.ps1')
$body = '{"type":"about:blank","title":"Not Found","status":404,"code":"resource.not_found","correlationId":"p01-smoke-fixture"}'
foreach ($content in @($body, [Text.Encoding]::UTF8.GetBytes($body))) {
    $response = @{StatusCode = 404; Headers = @{'Content-Type' = 'application/problem+json'; 'X-Correlation-ID' = 'p01-smoke-fixture'}; Content = $content}
    Assert-SafeMissingResponse $response 'p01-smoke-fixture'
}
$rejected = 0
foreach ($case in @('extra-field', 'wrong-status', 'wrong-type', 'wrong-code', 'wrong-body-correlation', 'wrong-header', 'wrong-media', 'wrong-http-status')) {
    $p = $body | ConvertFrom-Json
    $response = @{StatusCode = 404; Headers = @{'Content-Type' = 'application/problem+json'; 'X-Correlation-ID' = 'p01-smoke-fixture'}}
    switch ($case) {
        'extra-field' { $p | Add-Member -NotePropertyName detail -NotePropertyValue 'synthetic-internal-marker' }
        'wrong-status' { $p.status = 500 }
        'wrong-type' { $p.type = 'invalid' }
        'wrong-code' { $p.code = 'server.error' }
        'wrong-body-correlation' { $p.correlationId = 'other' }
        'wrong-header' { $response.Headers['X-Correlation-ID'] = 'other' }
        'wrong-media' { $response.Headers['Content-Type'] = 'text/plain' }
        'wrong-http-status' { $response.StatusCode = 500 }
    }
    $response.Content = [Text.Encoding]::UTF8.GetBytes(($p | ConvertTo-Json))
    $failed = $false
    try { Assert-SafeMissingResponse $response 'p01-smoke-fixture' } catch { $failed = $true }
    if (-not $failed) { throw "Unsafe smoke response accepted: $case" }
    $rejected++
}
Write-Output "PASS: string and byte-array ProblemDetails accepted; $rejected incorrect/leaking response contracts rejected. No containers or data changed."
