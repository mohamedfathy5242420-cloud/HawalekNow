param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$RulesPath = '',
    [string]$ReportPath = '',
    [string]$Image = '',
    [switch]$ContextOnly,
    [switch]$KeepImage
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Root = [IO.Path]::GetFullPath($Root)
$run = [guid]::NewGuid().ToString('N')
$parent = Join-Path $Root 'tmp'
$owned = Join-Path $parent ('docker-context-' + $run)
$context = Join-Path $owned 'context'
$export = Join-Path $owned 'visible-context'
$probe = Join-Path $owned 'artifact-probe'
$marker = 'HAWALEKNOW_NON_SECRET_DOCKER_FIXTURE_' + $run
$label = 'io.hawaleknow.context-test'
if (-not $RulesPath) { $RulesPath = Join-Path $Root '.dockerignore' }
if (-not $ReportPath) { $ReportPath = Join-Path $parent ('docker-context-' + $run + '.json') }
if (-not $Image) { $Image = 'hawaleknow/api-context-test:' + $run }
if ($Image -notmatch '^hawaleknow/[a-z0-9-]+:[a-z0-9-]+$') { throw 'Use an explicit HawalekNow test image tag.' }
$probeImage = 'hawaleknow/seed-artifact-test:' + $run
$receipt = [ordered]@{ run = $run; sourceHead = ''; rulesSha256 = ''; status = 'Failed'; context = 'Pending'; forbidden = @(); missing = @(); unexpected = @(); allowedCount = 0; forbiddenCount = 0; image = $Image; artifacts = 'NotRun' }
function Invoke-DockerChecked([string[]]$Arguments) {
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw ('Docker verification command failed: ' + $Arguments[0]) }
}
function Write-Fixture([string]$Relative) {
    $target = Join-Path $context $Relative
    [IO.Directory]::CreateDirectory((Split-Path $target -Parent)) | Out-Null
    [IO.File]::WriteAllText($target, $marker, [Text.UTF8Encoding]::new($false))
}
function Remove-OwnedImage([string]$Tag) {
    $owner = & docker image inspect --format ('{{index .Config.Labels "' + $label + '"}}') $Tag 2>$null
    if ($LASTEXITCODE -eq 0 -and $owner -eq $run) { Invoke-DockerChecked @('image', 'rm', $Tag) | Out-Null }
}
try {
    [IO.Directory]::CreateDirectory($context) | Out-Null
    [IO.File]::WriteAllText((Join-Path $owned 'owner'), $run)
    $receipt.sourceHead = (& git -C $Root -c "safe.directory=$Root" rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'A reviewed Git source tree is required.' }
    # Archive tracked blobs, not working-tree private settings, ignored files, or host directories.
    $archive = Join-Path $owned 'source.tar'
    & git -C $Root -c "safe.directory=$Root" archive --format=tar --output=$archive HEAD -- global.json backend/src
    if ($LASTEXITCODE -ne 0) { throw 'Clean source archive failed.' }
    & tar -xf $archive -C $context
    if ($LASTEXITCODE -ne 0) { throw 'Clean source extraction failed.' }
    Copy-Item -LiteralPath (Join-Path $Root 'backend/Dockerfile') -Destination (Join-Path $context 'backend/Dockerfile')
    Copy-Item -LiteralPath $RulesPath -Destination (Join-Path $context '.dockerignore')
    $receipt.rulesSha256 = (Get-FileHash -LiteralPath $RulesPath -Algorithm SHA256).Hash
    $seed = 'backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/'
    $seedNames = @('all-governorate.json', 'all-district.json', 'all-shiyakha.json')
    $seedPaths = @(($seedNames + @('SOURCE.md', 'LICENSE.txt')) | ForEach-Object { $seed + $_ })
    $allowed = @(Get-ChildItem -LiteralPath $context -Recurse -File -Force | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($context, $_.FullName).Replace('\', '/')
        if ($relative -eq 'global.json' -or $relative -in $seedPaths -or
            ($relative -match '^backend/src/.+/(?:[^/]+\.cs|[^/]+\.csproj|packages\.lock\.json|appsettings\.json|appsettings\.Development\.json)$')) { $relative }
    })
    if ($allowed.Count -lt 20 -or @($seedPaths | Where-Object { $_ -notin $allowed }).Count) { throw 'Required canonical build inputs were not staged.' }
    $names = @('appsettings.Local.json', 'appsettings.Development.Local.json', 'appsettings.Production.Local.json', 'appsettings.Staging.Local.json',
        '.env', '.env.local', '.env.Development', 'secrets.json', 'private.pfx', 'private.p12', 'private.key', 'private.pem', 'private.p8',
        'private.ppk', 'private.jks', 'private.keystore', 'private.pvk', 'private.snk', 'private.cer', 'private.crt', 'private.der', 'unapproved.json', 'unapproved.txt', 'private.dll')
    $forbidden = @()
    foreach ($directory in @('', 'backend/src/HawalekNow.Api/', 'backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/')) {
        foreach ($name in $names) { $relative = $directory + $name; Write-Fixture $relative; $forbidden += $relative }
    }
    foreach ($relative in @('backend/src/HawalekNow.Api/bin/Release/Generated.cs', 'backend/src/HawalekNow.Api/obj/Generated.cs',
            'backend/tests/PrivateTest.cs', 'docs/TARGET_SCHEDULE.md', '.git/config', '.aws/credentials', 'tmp/private.txt')) {
        Write-Fixture $relative; $forbidden += $relative
    }
    $receipt.allowedCount = $allowed.Count; $receipt.forbiddenCount = $forbidden.Count
    $captureFile = Join-Path $owned 'capture.Dockerfile'
    [IO.File]::WriteAllText($captureFile, "FROM scratch`nCOPY . /`n", [Text.UTF8Encoding]::new($false))
    # COPY all visible inputs: exercises Docker's matcher, not Git or a hand-written glob emulator.
    Invoke-DockerChecked @('build', '--progress=plain', '--file', $captureFile, '--output', ('type=local,dest=' + $export), $context)
    $visible = @(Get-ChildItem -LiteralPath $export -Recurse -File -Force | ForEach-Object { [IO.Path]::GetRelativePath($export, $_.FullName).Replace('\', '/') })
    $receipt.forbidden = @($forbidden | Where-Object { $_ -in $visible })
    $receipt.missing = @($allowed | Where-Object { $_ -notin $visible })
    $receipt.unexpected = @($visible | Where-Object { $_ -notin $allowed })
    $receipt.context = 'Failed'
    if ($receipt.forbidden.Count -or $receipt.missing.Count -or $receipt.unexpected.Count) {
        Write-Output ('Rejected context: forbidden=' + $receipt.forbidden.Count + '; missing=' + $receipt.missing.Count + '; unexpected=' + $receipt.unexpected.Count)
        foreach ($item in $receipt.forbidden) { Write-Output ('Forbidden fixture visible: ' + $item) }
        throw 'Docker context contract failed; non-sensitive fixture paths recorded in report.'
    }
    $receipt.context = 'Passed'
    Write-Output "PASS: Docker COPY received all $($allowed.Count) allowed inputs and none of $($forbidden.Count) forbidden fixture files; no extra files."
    if (-not $ContextOnly) {
        & docker image inspect $Image 1>$null 2>$null
        if ($LASTEXITCODE -eq 0) { throw 'Refusing to overwrite an existing image tag.' }
        Invoke-DockerChecked @('build', '--progress=plain', '--label', ($label + '=' + $run), '--tag', $Image, '--file', (Join-Path $context 'backend/Dockerfile'), $context)
        [IO.Directory]::CreateDirectory($probe) | Out-Null
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'verification/SeedArtifacts.cs') -Destination (Join-Path $probe 'Program.cs')
        [IO.File]::WriteAllText((Join-Path $probe 'ArtifactProbe.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
        $expected = @{ resources = @($seedNames | ForEach-Object { @{ name = 'HawalekNow.ReferenceSeed.' + $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $context ($seed + $_)) -Algorithm SHA256).Hash } });
            content = @(@('SOURCE.md', 'LICENSE.txt') | ForEach-Object { @{ path = 'Modules/Locations/SeedData/' + $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $context ($seed + $_)) -Algorithm SHA256).Hash } }) }
        $expected | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $probe 'expected.json') -Encoding utf8
        $sdk = [regex]::Match([IO.File]::ReadAllText((Join-Path $context 'backend/Dockerfile')), '(?m)^FROM (\S+) AS build\r?$').Groups[1].Value
        if ($sdk -notmatch '^mcr\.microsoft\.com/dotnet/sdk:[^\s]+@sha256:[0-9a-f]{64}$') { throw 'Expected the unchanged pinned SDK build image.' }
        $probeDockerfile = @"
FROM $Image AS published
FROM $sdk
WORKDIR /probe
COPY --from=published /app/ /published/
COPY . ./
RUN mkdir /empty && dotnet restore ArtifactProbe.csproj --source /empty \
    && dotnet run --project ArtifactProbe.csproj --configuration Release --no-restore -- /published expected.json $marker
"@
        [IO.File]::WriteAllText((Join-Path $probe 'Dockerfile'), $probeDockerfile, [Text.UTF8Encoding]::new($false))
        Invoke-DockerChecked @('build', '--progress=plain', '--network=none', '--label', ($label + '=' + $run), '--tag', $probeImage, $probe)
        $receipt.artifacts = 'Passed'
        $receipt.imageId = (& docker image inspect --format '{{.Id}}' $Image).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Could not record the verified image identity.' }
    }
    $receipt.status = 'Passed'
} finally {
    # Reports contain public paths, checksums and dummy-fixture metadata, never host settings.
    [IO.Directory]::CreateDirectory((Split-Path ([IO.Path]::GetFullPath($ReportPath)) -Parent)) | Out-Null
    $receipt | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $ReportPath -Encoding utf8
    if (-not $ContextOnly) {
        Remove-OwnedImage $probeImage
        if (-not $KeepImage) { Remove-OwnedImage $Image }
    }
    $resolved = [IO.Path]::GetFullPath($owned)
    $expectedParent = [IO.Path]::GetFullPath($parent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.File]::ReadAllText((Join-Path $resolved 'owner')) -ne $run) { throw 'Refusing cleanup outside the owned fixture.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    Write-Output ('Report: ' + $ReportPath)
}
