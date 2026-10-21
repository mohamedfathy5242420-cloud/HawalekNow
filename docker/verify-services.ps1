param([switch]$Persistence, [string]$ReceiptFile = (Join-Path $PSScriptRoot '../tmp/p01-s06-receipt.json'))
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/local.ps1" Validate
$composeArgs = @('compose','--env-file',"$PSScriptRoot/.env",'-f',"$PSScriptRoot/compose.yaml")
function Invoke-Sql([string]$Query, [int]$QueryTimeout = 150) {
    # SQL text enters stdin; password is resolved inside container, never an argv value.
    $result = $Query | & docker @composeArgs exec -T sql /bin/bash -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -l 15 -t "$1" -h -1 -W -m 1' s06-sql $QueryTimeout 2>&1
    if ($LASTEXITCODE -ne 0) {
        $sqlExit = $LASTEXITCODE
        $secret = ([IO.File]::ReadAllText("$PSScriptRoot/.env").Trim() -split '=',2)[1]
        $safeDiagnostic = ($result -join "`n").Replace($secret,'[REDACTED]')
        throw "SQL verification failed (exit $sqlExit): $safeDiagnostic. No cleanup performed."
    }
    return ($result -join "`n").Trim()
}
function Assert-Healthy {
    $deadline = [DateTime]::UtcNow.AddSeconds(240)
    do {
        $ready = $true
        foreach ($service in @('sql','mailpit')) {
            $id = & docker @composeArgs ps -q $service
            if ($LASTEXITCODE -ne 0 -or -not $id) { throw "Missing $service container." }
            # Read health fields only; full inspect includes secrets.
            $health = & docker inspect --format '{{.State.Health.Status}}' $id
            if ($LASTEXITCODE -ne 0) { throw 'Cannot read container health.' }
            if ($health -ne 'healthy') { $ready = $false }
        }
        if ($ready) { Write-Output 'PASS: both Docker Health Checks healthy.'; return }
        Start-Sleep -Seconds 3
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Health deadline exceeded; no cleanup performed.'
}
function Assert-Markers($Receipt) {
    $database = $Receipt.database
    $marker = $Receipt.marker
    if ($database -notmatch '^S06Probe_[a-f0-9]{32}$' -or $marker -notmatch '^[a-f0-9]{32}$') { throw 'Invalid task receipt; refusing SQL.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    $lastIssue = 'No matching SQL marker / Full Text result yet.'
    do {
        try {
            $answer = Invoke-Sql -Query "SET NOCOUNT ON; USE [$database]; SELECT COUNT(*) FROM dbo.Documents WHERE Id=1 AND Marker=N'$marker' AND CONTAINS(Body,N'`"saffron`"');" -QueryTimeout 10
            if ($answer -eq '1') { break }
            $lastIssue = 'SQL marker / Full Text result not ready.'
        } catch { $lastIssue = $_.Exception.Message }
        if ([DateTime]::UtcNow -ge $deadline) { throw "SQL persistence readiness deadline exceeded: $lastIssue" }
        Write-Warning "Waiting for SQL persistence query readiness: $lastIssue"
        Start-Sleep -Seconds 3
    } while ($true)
    $mail = Invoke-RestMethod "http://127.0.0.1:8025/api/v1/message/$($Receipt.mailId)"
    if ($mail.Subject -ne $Receipt.subject -or $mail.Text -notmatch [regex]::Escape($marker)) { throw 'Mailpit persistence marker missing.' }
    Write-Output 'PASS: SQL row, Full Text match and SMTP message markers retained.'
}
Assert-Healthy
foreach ($port in @(1433,1025,8025)) {
    $client = [Net.Sockets.TcpClient]::new()
    try {
        $connection = $client.ConnectAsync('127.0.0.1',$port)
        if (-not $connection.Wait(5000) -or -not $client.Connected) { throw "Loopback port $port unavailable." }
    } finally { $client.Dispose() }
}
Write-Output 'PASS: all three published loopback ports accept host connections.'
if (-not $Persistence) {
    if (Test-Path -LiteralPath $ReceiptFile) { throw 'Receipt already exists; use -Persistence or choose a new receipt. No existing database modified.' }
    $receiptDirectory = Split-Path ([IO.Path]::GetFullPath($ReceiptFile)) -Parent
    [IO.Directory]::CreateDirectory($receiptDirectory) | Out-Null
    $marker = [guid]::NewGuid().ToString('N')
    $database = 'S06Probe_' + $marker
    Write-Output "Creating task-owned probe database: $database (retained if a later check fails)."
    $version = Invoke-Sql "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)); SELECT FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'); SELECT 1;"
    Write-Output "SQL version / FullTextInstalled / SELECT 1: $version"
    if (-not $version.StartsWith('17.0.5015.4') -or $version -notmatch '(?m)^1\r?$') { throw 'Unexpected SQL or Full Text capability.' }
    $query = @"
SET NOCOUNT ON;
IF DB_ID(N'$database') IS NOT NULL THROW 51000, 'Probe name collision; refusing existing database.', 1;
CREATE DATABASE [$database];
GO
USE [$database];
CREATE TABLE dbo.Documents (Id int NOT NULL CONSTRAINT PK_Documents PRIMARY KEY, Body nvarchar(500) NOT NULL, Marker nvarchar(32) NOT NULL);
INSERT dbo.Documents VALUES(1,N'Local saffron verification',N'$marker'),(2,N'Unrelated document',N'$marker');
CREATE FULLTEXT CATALOG S06Catalog AS DEFAULT;
CREATE FULLTEXT INDEX ON dbo.Documents(Body LANGUAGE 1033) KEY INDEX PK_Documents WITH CHANGE_TRACKING AUTO;
DECLARE @deadline datetime2=DATEADD(second,120,SYSUTCDATETIME());
WHILE NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE CONTAINS(Body,N'"saffron"') AND Id=1)
BEGIN
 IF SYSUTCDATETIME() >= @deadline THROW 51001,'Full Text population timed out.',1;
 WAITFOR DELAY '00:00:01';
END;
IF (SELECT COUNT(*) FROM dbo.Documents WHERE CONTAINS(Body,N'"saffron"')) <> 1 THROW 51002,'Unexpected Full Text result.',1;
SELECT N'PASS: actual Full Text index populated; CONTAINS returned only Id=1';
GO
"@
    Write-Output (Invoke-Sql $query)
    $subject = "P01-S06 local probe $marker"
    $smtp = [Net.Mail.SmtpClient]::new('127.0.0.1',1025)
    $smtp.Timeout = 15000
    $message = [Net.Mail.MailMessage]::new('s06@example.invalid','local@example.invalid',$subject,"Non-sensitive local S06 marker $marker")
    try { $smtp.Send($message) } finally { $message.Dispose(); $smtp.Dispose() }
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $messages = Invoke-RestMethod 'http://127.0.0.1:8025/api/v1/messages'
        $found = @($messages.messages | Where-Object Subject -eq $subject)
        if ($found.Count -eq 1) { break }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($found.Count -ne 1) { throw 'SMTP message not received by Mailpit API within deadline.' }
    $receipt = [pscustomobject]@{database=$database;marker=$marker;subject=$subject;mailId=$found[0].ID}
    $receipt | ConvertTo-Json | Set-Content -LiteralPath $ReceiptFile -Encoding utf8
    Assert-Markers $receipt
    Write-Output "PASS: SMTP accepted and API verified. Task database: $database; receipt: $ReceiptFile"
    Write-Output 'Probe data retained for persistence checks. No database/message/volume deletion performed.'
} else {
    if (-not (Test-Path -LiteralPath $ReceiptFile)) { throw 'Missing persistence receipt; run initial probe first.' }
    $receipt = Get-Content -LiteralPath $ReceiptFile -Raw | ConvertFrom-Json
    Assert-Markers $receipt
    $before = @(& docker @composeArgs ps -q)
    & "$PSScriptRoot/local.ps1" Restart
    Assert-Healthy
    Assert-Markers $receipt
    & "$PSScriptRoot/local.ps1" Recreate
    Assert-Healthy
    $after = @(& docker @composeArgs ps -q)
    if ($before.Count -ne 2 -or $after.Count -ne 2 -or @($before | Where-Object { $_ -in $after }).Count -ne 0) { throw 'Expected two new container IDs.' }
    Assert-Markers $receipt
    Write-Output 'PASS: restart and container replacement preserved SQL and Mailpit data in named volumes.'
}
