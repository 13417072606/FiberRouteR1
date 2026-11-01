param([string]$Destination = (Join-Path $env:USERPROFILE 'Downloads\R1.5.2'))
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$baseUrl = 'https://github.com/13417072606/FiberRouteR1/releases/download/r1.5.2-download/'
$stem = 'FiberRouteR1-R1.5.2-program-source.zip'
$expected = 'FEB66E329C372055D95D607B0BFA59A0291CF2D59AB1798F0B906DA8BEA77C5A'
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
function Get-VerifiedFile([string]$name,[string]$hash) {
    $path = Join-Path $Destination $name
    if ((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $hash) { return $path }
    for ($attempt=1; $attempt -le 8; $attempt++) {
        try {
            $temp = $path + '.partial'
            if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force }
            Write-Host "下载 $name，第 $attempt 次..."
            & curl.exe --location --fail --retry 2 --connect-timeout 20 --max-time 180 --output $temp ($baseUrl + $name)
            if ($LASTEXITCODE -ne 0) { throw "curl 退出码 $LASTEXITCODE" }
            if ((Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash -ne $hash) { throw "SHA256 校验失败：$name" }
            Move-Item -LiteralPath $temp -Destination $path -Force
            return $path
        } catch {
            if ($attempt -eq 8) { throw }
            Start-Sleep -Seconds 3
        }
    }
}
$manifestName = 'R1.5.2-parts.json'
$manifestPath = Join-Path $Destination $manifestName
& curl.exe --location --fail --retry 3 --output $manifestPath ($baseUrl + $manifestName)
if ($LASTEXITCODE -ne 0) { throw '分卷清单下载失败' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.sha256 -ne $expected) { throw '分卷清单的整包 SHA256 与发布版本不符' }
$zipPath = Join-Path $Destination $stem
if (-not ((Test-Path -LiteralPath $zipPath) -and (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash -eq $expected)) {
    $stream = [IO.File]::Create($zipPath + '.partial')
    try {
        foreach ($part in $manifest.parts) {
            $partPath = Get-VerifiedFile $part.name $part.sha256
            $inputStream = [IO.File]::OpenRead($partPath)
            try { $inputStream.CopyTo($stream) } finally { $inputStream.Dispose() }
        }
    } finally { $stream.Dispose() }
    if ((Get-FileHash -LiteralPath ($zipPath + '.partial') -Algorithm SHA256).Hash -ne $expected) { throw '整包 SHA256 校验失败' }
    Move-Item -LiteralPath ($zipPath + '.partial') -Destination $zipPath -Force
}
Expand-Archive -LiteralPath $zipPath -DestinationPath $Destination -Force
Write-Host "已下载并校验：$zipPath"
Write-Host "解压目录：$(Join-Path $Destination '光路调度路由分析R1.5.2')"
