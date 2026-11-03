param([string]$Destination = (Join-Path $env:USERPROFILE 'Downloads\R1.5.4-Publisher-Tidy'))
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$base = 'https://github.com/13417072606/FiberRouteR1/releases/download/r1.5.4-publisher-tidy/'
$name = 'FiberRouteR1-Publisher-R1.5.4-tidy.zip'
$digest = '0A6628D4DC5E449509ADB6AB3CA8758C6D727E28AC06F7139BA79B3038DB8E81'
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$manifestFile = Join-Path $Destination 'R1.5.4-publisher-tidy-parts.json'
& curl.exe -fL --retry 5 -o $manifestFile ($base + 'R1.5.4-publisher-tidy-parts.json')
if ($LASTEXITCODE -ne 0) { throw 'Manifest download failed' }
$manifest = Get-Content -LiteralPath $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.sha256 -ne $digest) { throw 'Manifest hash mismatch' }
$zipFile = Join-Path $Destination $name
if (-not ((Test-Path -LiteralPath $zipFile) -and (Get-FileHash -LiteralPath $zipFile -Algorithm SHA256).Hash -eq $digest)) {
    $temp = $zipFile + '.partial'
    $output = [IO.File]::Create($temp)
    try {
        foreach ($part in $manifest.parts) {
            $path = Join-Path $Destination $part.name
            if (-not ((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $part.sha256)) {
                $ok = $false
                for ($attempt = 1; $attempt -le 8; $attempt++) {
                    $partial = $path + '.partial'
                    if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force }
                    & curl.exe -fL --retry 2 --connect-timeout 20 --max-time 180 -o $partial ($base + $part.name)
                    if ($LASTEXITCODE -eq 0 -and (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -eq $part.sha256) {
                        Move-Item -LiteralPath $partial -Destination $path -Force
                        $ok = $true
                        break
                    }
                    Start-Sleep -Seconds 3
                }
                if (-not $ok) { throw ('Part download failed: ' + $part.name) }
            }
            $inputFile = [IO.File]::OpenRead($path)
            try { $inputFile.CopyTo($output) } finally { $inputFile.Dispose() }
        }
    } finally { $output.Dispose() }
    if ((Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash -ne $digest) { throw 'ZIP hash mismatch' }
    Move-Item -LiteralPath $temp -Destination $zipFile -Force
}
Expand-Archive -LiteralPath $zipFile -DestinationPath $Destination -Force
Write-Host (Join-Path $Destination '管理方发布工具R1.5.4\管理方发布工具.exe')
