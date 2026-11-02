param(
    [ValidateSet('All','Client','Publisher','Source')][string]$Package = 'All',
    [string]$Destination = (Join-Path $env:USERPROFILE 'Downloads\R1.5.4-IconFix')
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$baseUrl = 'https://github.com/13417072606/FiberRouteR1/releases/download/r1.5.4-iconfix/'
$items = @(
    @{ Kind='Client'; Stem='FiberRouteR1-R1.5.4-full-client-iconfix.zip'; Digest='A99761DC5CD2220DC901D5E8498C1C2B7E5356728ABFCCE73EAFFB5D4B036E2A'; Manifest='R1.5.4-iconfix-full-client-parts.json' },
    @{ Kind='Source'; Stem='FiberRouteR1-R1.5.4-program-source-iconfix.zip'; Digest='6308143675B1479A580A7F9FFCF0A77AA3D69D21B104966FFB920DCE0761D8EF'; Manifest='R1.5.4-iconfix-program-source-parts.json' },
    @{ Kind='Publisher'; Stem='FiberRouteR1-Publisher-R1.5.4-iconfix.zip'; Digest='86ACEFB0860E35E36E8AACEE070EE300A6C3AC329FD3D0AB75C7DA7C6EB12FAA'; Manifest='R1.5.4-iconfix-publisher-parts.json' }
)
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
function Get-VerifiedPart([string]$name,[string]$hash) {
    $path = Join-Path $Destination $name
    if ((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $hash) { return $path }
    for ($attempt=1; $attempt -le 8; $attempt++) {
        try {
            $temp = $path + '.partial'
            if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force }
            Write-Host "Downloading $name, attempt $attempt..."
            & curl.exe --location --fail --retry 2 --connect-timeout 20 --max-time 180 --output $temp ($baseUrl + $name)
            if ($LASTEXITCODE -ne 0) { throw "curl exit code $LASTEXITCODE" }
            if ((Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash -ne $hash) { throw "Part SHA256 mismatch: $name" }
            Move-Item -LiteralPath $temp -Destination $path -Force
            return $path
        } catch {
            if ($attempt -eq 8) { throw }
            Start-Sleep -Seconds 3
        }
    }
}
foreach ($item in $items) {
    if ($Package -ne 'All' -and $Package -ne $item.Kind) { continue }
    $manifestPath = Join-Path $Destination $item.Manifest
    & curl.exe --location --fail --retry 3 --output $manifestPath ($baseUrl + $item.Manifest)
    if ($LASTEXITCODE -ne 0) { throw "Manifest download failed: $($item.Manifest)" }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.sha256 -ne $item.Digest) { throw "Manifest SHA256 mismatch: $($item.Stem)" }
    $zipPath = Join-Path $Destination $item.Stem
    if (-not ((Test-Path -LiteralPath $zipPath) -and (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash -eq $item.Digest)) {
        $tempZip = $zipPath + '.partial'
        $stream = [IO.File]::Create($tempZip)
        try {
            foreach ($part in $manifest.parts) {
                $partPath = Get-VerifiedPart $part.name $part.sha256
                $inputStream = [IO.File]::OpenRead($partPath)
                try { $inputStream.CopyTo($stream) } finally { $inputStream.Dispose() }
            }
        } finally { $stream.Dispose() }
        if ((Get-FileHash -LiteralPath $tempZip -Algorithm SHA256).Hash -ne $item.Digest) { throw "Package SHA256 mismatch: $($item.Stem)" }
        Move-Item -LiteralPath $tempZip -Destination $zipPath -Force
    }
    $extractTo = Join-Path $Destination $item.Kind
    New-Item -ItemType Directory -Path $extractTo -Force | Out-Null
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractTo -Force
    Write-Host "Verified: $zipPath"
    Write-Host "Extracted to: $extractTo"
}
Write-Host "Output directory: $Destination"
