param([Parameter(Mandatory)][string]$Image, [string]$Root = (Split-Path $PSScriptRoot -Parent), [string]$ReportPath = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Root = [IO.Path]::GetFullPath($Root)
if ($Image -notmatch '^hawaleknow/[a-z0-9-]+:[a-z0-9-]+$') { throw 'Use an explicit project-owned image tag.' }
function Invoke-StorageDocker([string[]]$Arguments) { & docker @Arguments; if ($LASTEXITCODE -ne 0) { throw 'Project storage Docker verification failed.' } }
$head = (& git -c "safe.directory=$Root" -C $Root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source head.' }
& git -c "safe.directory=$Root" -C $Root diff --quiet -- backend/src
if ($LASTEXITCODE -ne 0) { throw 'Commit the tested production source before verifying its image.' }
if (@(& git -c "safe.directory=$Root" -C $Root ls-files --others --exclude-standard -- backend/src).Count) { throw 'Untracked production source cannot verify an archived image.' }
$sourceHead = (& docker image inspect --format '{{index .Config.Labels "io.hawaleknow.source-head"}}' $Image).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceHead -ne $head) { throw 'The image must be built from this exact archived source head.' }
$imageUser = (& docker image inspect --format '{{.Config.User}}' $Image).Trim()
$storageUser = [regex]::Match($imageUser, '^([1-9][0-9]*)(?::[0-9]+)?$')
if (-not $storageUser.Success) { throw 'The reviewed image must have an explicit non-root numeric user.' }
$storageUid = $storageUser.Groups[1].Value
$run = [guid]::NewGuid().ToString('N')
$owned = Join-Path $Root ('tmp/store-files-' + $run)
$publish = Join-Path $owned 'publish'
[IO.Directory]::CreateDirectory($owned) | Out-Null
if (-not $ReportPath) { $ReportPath = Join-Path $owned 'verification.json' }
$volume = 'hawaleknow_p04_store_files_' + $run
$label = 'io.hawaleknow.storage-test'
$key = [guid]::NewGuid().ToString('N')
$receipt = [ordered]@{ status = 'Failed'; sourceHead = $head; image = $Image; volume = $volume; owner = $run; restart = 'NotRun'; permissions = 'NotRun'; hostRestart = 'NotRun'; volumePreserved = $true }
try {
    # The helper references the same committed Infrastructure project. It never loads API Program.
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'verification/StoreFiles.cs') -Destination (Join-Path $owned 'Program.cs')
    $project = Join-Path $owned 'StoreFiles.csproj'
    $reference = [Security.SecurityElement]::Escape((Join-Path $Root 'backend/src/HawalekNow.Infrastructure/HawalekNow.Infrastructure.csproj'))
    [IO.File]::WriteAllText($project, '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="' + $reference + '" /></ItemGroup></Project>')
    & dotnet publish $project --configuration Release --output $publish --disable-build-servers -m:1 *> (Join-Path $owned 'publish.log')
    if ($LASTEXITCODE -ne 0) { throw 'Storage runtime verifier build failed; inspect its isolated publish log.' }
    $hostRoot = Join-Path $owned 'host-images'
    $hostReceipt = Join-Path $owned 'host-receipt.json'
    foreach ($mode in @('write', 'read')) {
        & dotnet (Join-Path $publish 'StoreFiles.dll') $mode $hostRoot $key $hostReceipt
        if ($LASTEXITCODE -ne 0) { throw 'Storage did not survive the host process restart.' }
    }
    $receipt.hostRestart = 'Passed'
    Invoke-StorageDocker @('volume', 'create', '--label', ($label + '=' + $run), $volume) | Out-Null
    $owner = (& docker volume inspect --format ('{{index .Labels "' + $label + '"}}') $volume).Trim()
    if ($owner -ne $run) { throw 'Refusing to initialize an unowned storage volume.' }
    $mount = 'type=volume,source=' + $volume + ',target=/storage'
    # Only the newly labeled project volume is initialized. Its contents are preserved after verification.
    Invoke-StorageDocker @('run', '--rm', '--network', 'none', '--read-only', '--user', '0', '--cap-drop', 'ALL', '--cap-add', 'CHOWN', '--cap-add', 'FOWNER',
        '--mount', $mount, '--entrypoint', 'sh', $Image, '-c', ('chown ' + $storageUid + ':' + $storageUid + ' /storage && chmod 700 /storage')) | Out-Null
    $bind = 'type=bind,source=' + $publish + ',target=/probe,readonly'
    foreach ($mode in @('write', 'read')) {
        Invoke-StorageDocker @('run', '--rm', '--network', 'none', '--read-only', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges', '--memory', '256m',
            '--mount', $mount, '--mount', $bind, '--tmpfs', '/tmp:rw,nosuid,noexec,size=64m', '--entrypoint', 'dotnet', $Image,
            '/probe/StoreFiles.dll', $mode, '/storage/images', $key, '/storage/receipt.json')
    }
    $receipt.restart = 'Passed'
    Invoke-StorageDocker @('run', '--rm', '--network', 'none', '--read-only', '--user', '65534:65534', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
        '--mount', ($mount + ',readonly'), '--entrypoint', 'sh', $Image, '-c', 'test ! -r "$1" && test ! -w /storage/images', '--', ('/storage/images/' + $key + '.png'))
    $receipt.permissions = 'Passed: owner modes0700/0600 and an unrelated UID65534 cannot read the known existing image or write its directory.'
    $receipt.status = 'Passed'
    Write-Output 'PASS: actual host/container process restarts preserve store files; unrelated Linux UID denied; owned volume retained.'
} finally {
    [IO.File]::WriteAllText($ReportPath, ($receipt | ConvertTo-Json -Depth 10) + [Environment]::NewLine)
}
