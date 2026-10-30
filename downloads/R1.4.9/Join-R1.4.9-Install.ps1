$ErrorActionPreference = 'Stop'
$outputName = 'FiberRouteR1-R1.4.9-install.zip'
$expectedSha = '50ad3fe1946db80b24c4bb2019a9e6cfdcec28979d2cfc5d017d081c07b41efb'
$parts = @(
    @{ Name = 'FiberRouteR1-R1.4.9-install.zip.001'; Size = 15728640; Sha256 = '780c83c389e17e27e56dc17bd9ed1cbe50d087f44277191e4df383533b857c01' }
    @{ Name = 'FiberRouteR1-R1.4.9-install.zip.002'; Size = 7587678; Sha256 = 'f3fb63d1ef94602f7d83a380667b2454d063e69c33242b2308b8913ac94a70e6' }
)
$folder = Split-Path -Parent $MyInvocation.MyCommand.Path
$output = Join-Path $folder $outputName
$temporary = $output + '.partial'
if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
try {
    $writer = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    try {
        foreach ($item in $parts) {
            $path = Join-Path $folder $item.Name
            if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw ('缺少分包：' + $item.Name) }
            if ((Get-Item -LiteralPath $path).Length -ne $item.Size) { throw ('分包大小不符：' + $item.Name) }
            if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $item.Sha256) { throw ('分包校验失败：' + $item.Name) }
            $reader = [IO.File]::OpenRead($path)
            try { $reader.CopyTo($writer) } finally { $reader.Dispose() }
        }
    } finally { $writer.Dispose() }
    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $expectedSha) { throw '合并后的安装包校验失败' }
    Move-Item -LiteralPath $temporary -Destination $output -Force
    Write-Host ('合并完成：' + $output) -ForegroundColor Green
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
