function Get-ApiResponseText($Response) {
    if ($Response.Content -is [byte[]]) { return [Text.Encoding]::UTF8.GetString($Response.Content) }
    return [string]$Response.Content
}
function Assert-SafeMissingResponse($Response, [string]$Correlation) {
    # Some PowerShell versions return application/problem+json as byte[]. Never pipe the
    # bytes into ConvertFrom-Json: that parses each byte as a separate JSON number.
    $problem = ConvertFrom-Json -InputObject (Get-ApiResponseText $Response)
    $keys = @($problem.PSObject.Properties.Name | Sort-Object)
    if ($Response.StatusCode -ne 404 -or $Response.Headers['Content-Type'] -notmatch '^application/problem\+json' -or ($keys -join ',') -ne 'code,correlationId,status,title,type' -or $problem.status -ne 404 -or $problem.type -ne 'about:blank' -or $problem.code -ne 'resource.not_found' -or $problem.correlationId -ne $Correlation -or ($Response.Headers['X-Correlation-ID'] -join '') -ne $problem.correlationId) {
        throw 'Docker safe 404 contract failed.'
    }
}
