param(
    [ValidateSet('All', 'Client', 'Publisher')][string]$Package = 'All',
    [string]$Destination = (Join-Path $env:USERPROFILE 'Downloads\R1.5.5')
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$baseUrl = 'https://github.com/13417072606/FiberRouteR1/releases/download/r1.5.5-download/'
New-Item -ItemType Directory -Path $Destination -Force | Out-Null

function Download-File([string]$url, [string]$path) {
    $temporary = "$path.partial"
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    try {
        & curl.exe --fail --location --retry 4 --connect-timeout 20 --output $temporary $url
        if ($LASTEXITCODE -ne 0) { throw "Download failed: $url (curl exit code $LASTEXITCODE)" }
        Move-Item -LiteralPath $temporary -Destination $path -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}

$manifestPath = Join-Path $Destination 'R1.5.5-download.json'
Download-File ($baseUrl + 'R1.5.5-download.json') $manifestPath
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.version -ne '1.5.5') { throw 'Unexpected release manifest version.' }

foreach ($item in $manifest.packages) {
    if ($Package -ne 'All' -and $Package -ne $item.kind) { continue }
    $target = Join-Path $Destination $item.name
    if (-not ((Test-Path -LiteralPath $target) -and
        (Get-Item -LiteralPath $target).Length -eq [long]$item.size -and
        (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $item.sha256)) {
        Download-File ($baseUrl + [Uri]::EscapeDataString($item.name)) $target
    }
    if ((Get-Item -LiteralPath $target).Length -ne [long]$item.size -or
        (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $item.sha256) {
        Remove-Item -LiteralPath $target -Force
        throw "SHA256 verification failed: $($item.name)"
    }
    $extractTo = Join-Path $Destination $item.kind
    New-Item -ItemType Directory -Path $extractTo -Force | Out-Null
    Expand-Archive -LiteralPath $target -DestinationPath $extractTo -Force
    Write-Host "Verified and extracted: $extractTo"
}
