param(
    [ValidateSet('Both','Client','Publisher')][string]$Package = 'Both',
    [string]$Destination = (Join-Path $env:USERPROFILE 'Downloads\R1.5.4')
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$baseUrl = 'https://github.com/13417072606/FiberRouteR1/releases/download/r1.5.4-download/'
$items = @(
    @{ Kind='Client'; Stem='FiberRouteR1-R1.5.4-full-client.zip'; Digest='A41E9D02F973F126A31BB15DDB74679D87FBDE32AA90DBA2B0748C8058080405'; Manifest='R1.5.4-full-client-parts.json' },
    @{ Kind='Publisher'; Stem='FiberRouteR1-Publisher-R1.5.4.zip'; Digest='1F08876A267E7603C1F58D9E8E7CBE0517E63AAFBB26937E23084BD67C8A5437'; Manifest='R1.5.4-publisher-parts.json' }
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
    if ($Package -ne 'Both' -and $Package -ne $item.Kind) { continue }
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
    Expand-Archive -LiteralPath $zipPath -DestinationPath $Destination -Force
    Write-Host "Verified and extracted: $zipPath"
}
Write-Host "Output directory: $Destination"
